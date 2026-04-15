namespace TransactionSign.Tests.ConcurrencySimulation;

public class SimulationResults
{
    public List<TransactionSummary> Transactions { get; } = new();
    public int RequiredSignatures { get; set; }
    public int TotalSignatures { get; set; }
    public int TotalFinalizations { get; set; }
    public int TotalSettlements { get; set; }
    public int TotalFees { get; set; }
}
