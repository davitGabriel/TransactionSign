namespace TransactionSign.Application.Exceptions;

public sealed class SignatureLimitReachedException : Exception
{
    public int TransactionId { get; }
    public int RequiredSignatures { get; }

    public SignatureLimitReachedException(int transactionId, int requiredSignatures)
        : base($"Transaction {transactionId} already has the required {requiredSignatures} signatures.")
    {
        TransactionId = transactionId;
        RequiredSignatures = requiredSignatures;
    }
}
