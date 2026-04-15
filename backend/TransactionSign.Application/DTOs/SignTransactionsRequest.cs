namespace TransactionSign.Application.DTOs;

public class SignTransactionsRequest
{
    public List<int> TransactionIds { get; set; } = new();
}
