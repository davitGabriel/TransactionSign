namespace TransactionSign.Application.DTOs;

public class CompletedTransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal SettlementAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}
