namespace TransactionSign.Application.DTOs;

public class SignResult
{
    public int TransactionId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
