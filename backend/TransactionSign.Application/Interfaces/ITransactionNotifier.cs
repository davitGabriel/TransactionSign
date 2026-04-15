namespace TransactionSign.Application.Interfaces;

public interface ITransactionNotifier
{
    Task NotifyTransactionSignedAsync(int transactionId, int signatureCount, int requiredSignatures);
    Task NotifyTransactionFinalizedAsync(int transactionId, decimal fee, decimal settlement);
}
