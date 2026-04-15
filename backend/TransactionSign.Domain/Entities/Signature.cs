namespace TransactionSign.Domain.Entities;

public class Signature : BaseEntity
{
    public int TransactionId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Transaction Transaction { get; set; } = null!;
}
