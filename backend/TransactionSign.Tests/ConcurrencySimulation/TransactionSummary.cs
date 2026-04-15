namespace TransactionSign.Tests.ConcurrencySimulation;

public class TransactionSummary
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public int SignatureCount { get; set; }
    public int UniqueSigners { get; set; }
    public int FinalizationCount { get; set; }
    public int SettlementCount { get; set; }
    public int FeeCount { get; set; }
}
