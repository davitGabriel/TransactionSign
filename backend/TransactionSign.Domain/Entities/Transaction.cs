using System.ComponentModel.DataAnnotations;
using TransactionSign.Domain.Enums;

namespace TransactionSign.Domain.Entities;

public class Transaction : BaseEntity
{
    public int? ParentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsDebit { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public string CurrencyId { get; set; } = "EUR";
    public string? Note { get; set; }
    public int? AgentId { get; set; }
    public DateTime ValueDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public string? Reason { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public InternalStatus InternalStatus { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<Signature> Signs { get; set; } = new List<Signature>();
    public TransactionFinalization? Finalization { get; set; }
}
