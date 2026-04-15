namespace TransactionSign.Domain.Entities;

public class Settlement : BaseEntity
{
    public int TransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public Transaction Transaction { get; set; } = null!;
}
