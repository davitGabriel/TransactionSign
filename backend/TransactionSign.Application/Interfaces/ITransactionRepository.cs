using TransactionSign.Domain.Entities;

namespace TransactionSign.Application.Interfaces;

public interface ITransactionRepository
{
    Task<List<Transaction>> GetPendingTransactionsAsync();
    Task<List<(Transaction Transaction, decimal Fee, decimal Settlement)>> GetCompletedTransactionsWithDetailsAsync();
    Task<Transaction?> GetByIdAsync(int id);
    Task<int> GetRequiredSignaturesAsync();
    Task<int> GetSignatureCountAsync(int transactionId);
    Task<bool> HasUserSignedAsync(int transactionId, int userId);
    Task AddSignatureAsync(Signature sign);
    Task<bool> TryFinalizeTransactionAsync(Transaction transaction, decimal fee, decimal settlement);
    Task SaveChangesAsync();
    Task RestoreAllTransactionsAsync();
}
