using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionSign.Application.Constants;
using TransactionSign.Application.DTOs;
using TransactionSign.Application.Exceptions;
using TransactionSign.Application.Interfaces;
using TransactionSign.Domain.Entities;
using TransactionSign.Domain.Enums;

namespace TransactionSign.Application.Services;

public class TransactionService
{
    private readonly ITransactionRepository _repository;
    private readonly ILogger<TransactionService> _logger;
    private readonly ITransactionNotifier? _notifier;

    public TransactionService(
        ITransactionRepository repository,
        ILogger<TransactionService>? logger = null,
        ITransactionNotifier? notifier = null)
    {
        _repository = repository;
        _logger = logger ?? NullLogger<TransactionService>.Instance;
        _notifier = notifier;
    }

    public async Task<List<TransactionDto>> GetPendingTransactionsAsync(int userId)
    {
        List<Transaction> transactions = await _repository.GetPendingTransactionsAsync();
        int requiredSignatures = await _repository.GetRequiredSignaturesAsync();

        List<TransactionDto> result = new List<TransactionDto>();
        foreach (Transaction t in transactions)
        {
            int signCount = await _repository.GetSignatureCountAsync(t.Id);
            bool hasUserSigned = await _repository.HasUserSignedAsync(t.Id, userId);

            result.Add(new TransactionDto
            {
                Id = t.Id,
                Type = t.Type,
                ValueDate = t.ValueDate,
                LastModifiedDate = t.LastModifiedDate,
                Reason = t.Reason,
                Company = t.Company,
                Counterparty = t.Counterparty,
                Amount = t.Amount,
                Status = (int)t.Status,
                InternalStatus = (int)t.InternalStatus,
                SignatureCount = signCount,
                RequiredSignatures = requiredSignatures,
                CanSign = !hasUserSigned && signCount < requiredSignatures
            });
        }

        return result;
    }

    public async Task<List<CompletedTransactionDto>> GetCompletedTransactionsAsync()
    {
        List<(Transaction Transaction, decimal Fee, decimal Settlement)> data = await _repository.GetCompletedTransactionsWithDetailsAsync();
        return data.Select(d => new CompletedTransactionDto
        {
            Id = d.Transaction.Id,
            Amount = d.Transaction.Amount,
            Fee = d.Fee,
            SettlementAmount = d.Settlement,
            Status = "Completed"
        }).ToList();
    }

    public async Task<List<SignResult>> SignTransactionsAsync(List<int> transactionIds, int userId)
    {
        List<SignResult> results = new List<SignResult>();

        foreach (var txnId in transactionIds)
        {
            SignResult result = await SignSingleTransactionAsync(txnId, userId);
            results.Add(result);
        }

        return results;
    }

    private async Task<SignResult> SignSingleTransactionAsync(int transactionId, int userId)
    {
        Transaction? transaction = await _repository.GetByIdAsync(transactionId);
        if (transaction == null)
        {
            return new SignResult { TransactionId = transactionId, Success = false, Error = "Transaction not found" };
        }

        if (transaction.Status != TransactionStatus.Internal || transaction.InternalStatus != InternalStatus.ToSign)
        {
            // User attempted to sign already finalized transaction
            _logger.LogWarning("REJECTED: UserId={UserId} attempted to sign already finalized TxId={TransactionId}",
                userId, transactionId);
            return new SignResult { TransactionId = transactionId, Success = false, Error = "Transaction not eligible for signing, Already Finalized!" };
        }

        if (await _repository.HasUserSignedAsync(transactionId, userId))
        {
            // User already signed this transaction
            _logger.LogWarning("REJECTED: UserId={UserId} already signed TxId={TransactionId}",
                userId, transactionId);
            return new SignResult { TransactionId = transactionId, Success = false, Error = "Already signed" };
        }

        Signature sign = new Signature
        {
            TransactionId = transactionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _repository.AddSignatureAsync(sign);
        }
        catch (InvalidOperationException ex) when (ex.Message == ErrorMessages.AlreadySigned)
        {
            _logger.LogWarning("REJECTED: UserId={UserId} already signed TxId={TransactionId} (concurrent)",
                userId, transactionId);
            return new SignResult { TransactionId = transactionId, Success = false, Error = ErrorMessages.AlreadySigned };
        }
        catch (SignatureLimitReachedException)
        {
            _logger.LogWarning("REJECTED: UserId={UserId} attempted to sign TxId={TransactionId} but signature limit reached",
                userId, transactionId);
            return new SignResult { TransactionId = transactionId, Success = false, Error = "Signature limit reached" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Failed to add signature for TxId={TransactionId}, UserId={UserId}",
                transactionId, userId);
            return new SignResult { TransactionId = transactionId, Success = false, Error = $"Error adding signature: {ex.Message}" };
        }

        int signatureCount = await _repository.GetSignatureCountAsync(transactionId);
        int requiredSignatures = await _repository.GetRequiredSignaturesAsync();

        // Notify all connected clients about the new signature
        if (_notifier != null)
        {
            await _notifier.NotifyTransactionSignedAsync(transactionId, signatureCount, requiredSignatures);
        }

        if (signatureCount >= requiredSignatures)
        {
            decimal fee = CalculateFee(transaction.Amount);
            decimal settlement = fee * 0.05m;
            bool finalized = await _repository.TryFinalizeTransactionAsync(transaction, fee, settlement);

            // Notify all connected clients about the finalization
            if (finalized && _notifier != null)
            {
                await _notifier.NotifyTransactionFinalizedAsync(transactionId, fee, settlement);
            }
        }

        return new SignResult { TransactionId = transactionId, Success = true };
    }

    private static decimal CalculateFee(decimal amount)
    {
        decimal rate;
        if (amount < 10000)
            rate = 0.003m;
        else if (amount < 50000)
            rate = 0.002m;
        else
            rate = 0.001m;

        decimal fee = amount * rate;
        return Math.Clamp(fee, 50, 1800);
    }

    public async Task RestoreAllTransactionsAsync()
    {
        await _repository.RestoreAllTransactionsAsync();
    }
}
