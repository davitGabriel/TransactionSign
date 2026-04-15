using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionSign.Application.Constants;
using TransactionSign.Application.Exceptions;
using TransactionSign.Application.Interfaces;
using TransactionSign.Domain.Entities;
using TransactionSign.Domain.Enums;
using TransactionSign.Infrastructure.Data;

namespace TransactionSign.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionRepository> _logger;

    public TransactionRepository(AppDbContext context, ILogger<TransactionRepository>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<TransactionRepository>.Instance;
    }

    public async Task<List<Transaction>> GetPendingTransactionsAsync()
    {
        return await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Internal && t.InternalStatus == InternalStatus.ToSign)
            .ToListAsync();
    }

    public async Task<List<(Transaction Transaction, decimal Fee, decimal Settlement)>> GetCompletedTransactionsWithDetailsAsync()
    {
        List<Transaction> completedTxns = await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Completed && t.Type != "Fee")
            .ToListAsync();

        List<(Transaction, decimal, decimal)> result = new List<(Transaction, decimal, decimal)>();
        foreach (var txn in completedTxns)
        {
            decimal settlement = await _context.Settlements
                .Where(s => s.TransactionId == txn.Id)
                .Select(s => s.Amount)
                .FirstOrDefaultAsync();

            decimal fee = await _context.Transactions
                .Where(t => t.Type == "Fee" && t.ParentId == txn.Id)
                .Select(t => t.Amount)
                .FirstOrDefaultAsync();

            result.Add((txn, fee, settlement));
        }

        return result;
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        return await _context.Transactions.FindAsync(id);
    }

    public async Task<int> GetRequiredSignaturesAsync()
    {
        SiteSetting? setting = await _context.SiteSettings
            .FirstOrDefaultAsync(s =>
                s.Key == "NumberOfRequiredAmSignatures" ||
                s.Key == "RequiredSignatures");
        return setting != null ? int.Parse(setting.Value) : 2;
    }

    public async Task<int> GetSignatureCountAsync(int transactionId)
    {
        return await _context.Signatures
            .CountAsync(s => s.TransactionId == transactionId);
    }

    public async Task<bool> HasUserSignedAsync(int transactionId, int userId)
    {
        return await _context.Signatures
            .AnyAsync(s => s.TransactionId == transactionId && s.UserId == userId);
    }

    public async Task AddSignatureAsync(Signature sign)
    {
        // Read required signatures outside transaction - this value rarely changes
        int requiredSignatures = await GetRequiredSignaturesAsync();

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();

            // Use ReadCommitted instead of Serializable to reduce lock contention
            await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                // UPDLOCK + HOLDLOCK: Lock rows for this TransactionId until transaction commits
                // - UPDLOCK: Prevents other transactions from acquiring update/exclusive locks
                // - HOLDLOCK: Holds the lock until this transaction commits (prevents phantom reads)
                // This serializes concurrent signatures for the SAME transaction
                // while allowing signatures for DIFFERENT transactions to proceed in parallel
                int currentSignatureCount = await _context.Signatures
                    .FromSqlRaw(
                        "SELECT * FROM Signatures WITH (UPDLOCK, HOLDLOCK) WHERE TransactionId = {0}",
                        sign.TransactionId)
                    .CountAsync();

                if (currentSignatureCount >= requiredSignatures)
                {
                    throw new SignatureLimitReachedException(sign.TransactionId, requiredSignatures);
                }

                // No need to check for duplicate user signature here - unique constraint handles it
                _context.Signatures.Add(new Signature
                {
                    TransactionId = sign.TransactionId,
                    UserId = sign.UserId,
                    CreatedAt = sign.CreatedAt
                });

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                int newCount = currentSignatureCount + 1;
                _logger.LogInformation("SIGNED: TxId={TransactionId} UserId={UserId} ({SignatureCount}/{RequiredSignatures})",
                    sign.TransactionId, sign.UserId, newCount, requiredSignatures);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Unique constraint violation means user already signed (concurrent request)
                await dbTransaction.RollbackAsync();
                throw new InvalidOperationException(ErrorMessages.AlreadySigned);
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<bool> TryFinalizeTransactionAsync(Transaction transaction, decimal fee, decimal settlement)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();

            Transaction? trackedTransaction = await _context.Transactions.FindAsync(transaction.Id);
            if (trackedTransaction == null)
            {
                return false;
            }

            List<int> signedUserIds = await _context.Signatures
                .Where(s => s.TransactionId == transaction.Id)
                .Select(s => s.UserId)
                .ToListAsync();

            using IDbContextTransaction dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                TransactionFinalization finalization = new TransactionFinalization
                {
                    TransactionId = trackedTransaction.Id,
                    FinalizedAt = DateTime.UtcNow
                };
                _context.TransactionFinalizations.Add(finalization);

                trackedTransaction.Status = TransactionStatus.Completed;
                trackedTransaction.InternalStatus = InternalStatus.Completed;
                trackedTransaction.LastModifiedDate = DateTime.UtcNow;

                Transaction feeTransaction = new Transaction
                {
                    ParentId = trackedTransaction.Id,
                    Type = "Fee",
                    IsDebit = trackedTransaction.IsDebit,
                    CreateDate = DateTime.UtcNow,
                    ValueDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow,
                    Reason = $"Fee for transaction {trackedTransaction.Id}",
                    Company = trackedTransaction.Company,
                    Counterparty = "NA",
                    Amount = fee,
                    CurrencyId = trackedTransaction.CurrencyId,
                    Note = null,
                    AgentId = trackedTransaction.AgentId,
                    Status = TransactionStatus.Completed,
                    InternalStatus = InternalStatus.Completed
                };
                _context.Transactions.Add(feeTransaction);

                Settlement settlementRecord = new Settlement
                {
                    TransactionId = trackedTransaction.Id,
                    Amount = settlement,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Settlements.Add(settlementRecord);

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("FINALIZED: TxId={TransactionId} SignedBy=[{SignedUserIds}]",
                    transaction.Id, string.Join(",", signedUserIds));
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await dbTransaction.RollbackAsync();
                return false;
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        });
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        return ex.InnerException?.Message.Contains("UNIQUE constraint") == true
            || ex.InnerException?.Message.Contains("duplicate key") == true
            || ex.InnerException?.Message.Contains("Cannot insert duplicate") == true;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task RestoreAllTransactionsAsync()
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using IDbContextTransaction dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.TransactionFinalizations.ExecuteDeleteAsync();
                await _context.Signatures.ExecuteDeleteAsync();
                await _context.Settlements.ExecuteDeleteAsync();

                await _context.Transactions
                    .Where(t => t.Type == "Fee")
                    .ExecuteDeleteAsync();

                await _context.Transactions
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.Status, TransactionStatus.Internal)
                        .SetProperty(t => t.InternalStatus, InternalStatus.ToSign)
                        .SetProperty(t => t.LastModifiedDate, DateTime.UtcNow));

                await dbTransaction.CommitAsync();
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        });
    }
}
