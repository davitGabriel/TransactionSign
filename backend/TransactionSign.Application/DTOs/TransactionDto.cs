namespace TransactionSign.Application.DTOs;

public class TransactionDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime ValueDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public string? Reason { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public int InternalStatus { get; set; }
    public int SignatureCount { get; set; }
    public int RequiredSignatures { get; set; }
    public bool CanSign { get; set; }
}
