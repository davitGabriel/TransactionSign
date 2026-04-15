namespace TransactionSign.Domain.Entities;

public class TransactionFinalization : BaseEntity
{
    public int TransactionId { get; set; }
    public DateTime FinalizedAt { get; set; }

    public Transaction Transaction { get; set; } = null!;
}
