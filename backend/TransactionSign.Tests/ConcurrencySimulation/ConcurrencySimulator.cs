using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransactionSign.Application.DTOs;
using TransactionSign.Application.Services;
using TransactionSign.Domain.Entities;
using TransactionSign.Infrastructure.Data;
using TransactionSign.Infrastructure.Repositories;

namespace TransactionSign.Tests.ConcurrencySimulation;

/// <summary>
/// Integration Test
/// This test can be run against a local DB to simulate concurrent transaction signing.
/// Console-based concurrency simulation for testing transaction signing under load.
/// Run with: dotnet run --project TransactionSign.Tests
/// </summary>
public static class ConcurrencySimulator
{
    private static readonly ConcurrentDictionary<int, int> SignatureAttemptsByTransaction = new();
    private static readonly ConcurrentDictionary<int, int> SuccessfulSignsByTransaction = new();
    private static readonly ConcurrentDictionary<int, int> FailedSignsByTransaction = new();
    private static readonly ConcurrentDictionary<string, int> ErrorCounts = new();

    private static int _totalAttempts;
    private static int _totalSuccesses;
    private static int _totalFailures;

    private static ILoggerFactory? _loggerFactory;

    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("Transaction Sign - Concurrency Simulation");
        Console.WriteLine("============================================================");
        Console.WriteLine();

        IConfigurationRoot configuration = LoadConfiguration();
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAIL: ConnectionStrings:DefaultConnection is missing in appsettings.json.");
            Console.ResetColor();
            return 1;
        }

        SimulationOptions options = SimulationOptions.FromConfiguration(configuration);
        int concurrentUsers = Random.Shared.Next(options.MinConcurrentUsers, options.MaxConcurrentUsers + 1);

        // Create logger factory for console output
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss.fff ";
                    options.IncludeScopes = false;
                });
        });

        Console.WriteLine("Simulation Configuration");
        Console.WriteLine($"- Users: {concurrentUsers} (range: {options.MinConcurrentUsers}-{options.MaxConcurrentUsers})");
        Console.WriteLine($"- Iterations per user: {options.IterationsPerUser}");
        Console.WriteLine($"- Delay between actions: {options.MinDelayMs}-{options.MaxDelayMs} ms");
        Console.WriteLine();

        

        Console.WriteLine("Press any key to start...");
        Console.ReadKey();

        //Comment to combine with real time testing
        //Console.WriteLine("Resetting database...");
        //await ResetDatabaseAsync(connectionString);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await RunConcurrentSimulationAsync(connectionString, concurrentUsers, options);
        stopwatch.Stop();
        Console.WriteLine($"Completed in {stopwatch.ElapsedMilliseconds} ms.");
        Console.WriteLine();

        SimulationResults results = await QueryResultsAsync(connectionString);

        PrintSimulationSummary();
        PrintDatabaseResults(results);

        bool passed = ValidateConsistency(results);
        Console.WriteLine();
        Console.WriteLine($"RESULT: {(passed ? "PASS" : "FAIL")}");

        _loggerFactory?.Dispose();

        Console.ReadKey();

        return passed ? 0 : 1;
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    private static async Task ResetDatabaseAsync(string connectionString)
    {
        await using var context = CreateDbContext(connectionString);
        var repoLogger = _loggerFactory?.CreateLogger<TransactionRepository>();
        TransactionRepository repository = new TransactionRepository(context, repoLogger);
        await repository.RestoreAllTransactionsAsync();
    }

    private static async Task RunConcurrentSimulationAsync(string connectionString, int userCount, SimulationOptions options)
    {
        Task[] tasks = Enumerable.Range(1, userCount)
            .Select(userId => SimulateUserAsync(connectionString, userId, options))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private static async Task SimulateUserAsync(string connectionString, int userId, SimulationOptions options)
    {
        Random random = new Random(HashCode.Combine(userId, Environment.TickCount, Guid.NewGuid()));

        for (var iteration = 1; iteration <= options.IterationsPerUser; iteration++)
        {
            try
            {
                await Task.Delay(random.Next(options.MinDelayMs, options.MaxDelayMs + 1));

                List<int> signableIds = await GetSignableTransactionIdsAsync(connectionString, userId);
                if (signableIds.Count == 0) continue;

                int selectedId = signableIds[random.Next(signableIds.Count)];
                Interlocked.Increment(ref _totalAttempts);
                SignatureAttemptsByTransaction.AddOrUpdate(selectedId, 1, (_, c) => c + 1);

                await Task.Delay(random.Next(options.MinDelayMs, options.MaxDelayMs + 1));

                SignResult? result = await SignTransactionAsync(connectionString, userId, selectedId);
                if (result?.Success == true)
                {
                    Interlocked.Increment(ref _totalSuccesses);
                    SuccessfulSignsByTransaction.AddOrUpdate(selectedId, 1, (_, c) => c + 1);
                }
                else
                {
                    Interlocked.Increment(ref _totalFailures);
                    FailedSignsByTransaction.AddOrUpdate(selectedId, 1, (_, c) => c + 1);
                    ErrorCounts.AddOrUpdate(result?.Error ?? "Unknown", 1, (_, c) => c + 1);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _totalFailures);
                ErrorCounts.AddOrUpdate($"Exception: {ex.GetType().Name}", 1, (_, c) => c + 1);
            }
        }
    }

    private static async Task<List<int>> GetSignableTransactionIdsAsync(string connectionString, int userId)
    {
        await using var context = CreateDbContext(connectionString);
        var repoLogger = _loggerFactory?.CreateLogger<TransactionRepository>();
        var serviceLogger = _loggerFactory?.CreateLogger<TransactionService>();
        TransactionRepository repository = new TransactionRepository(context, repoLogger);
        TransactionService service = new TransactionService(repository, serviceLogger);
        List<TransactionDto> pending = await service.GetPendingTransactionsAsync(userId);
        return pending.Where(t => t.CanSign).Select(t => t.Id).ToList();
    }

    private static async Task<SignResult?> SignTransactionAsync(string connectionString, int userId, int transactionId)
    {
        await using var context = CreateDbContext(connectionString);
        var repoLogger = _loggerFactory?.CreateLogger<TransactionRepository>();
        var serviceLogger = _loggerFactory?.CreateLogger<TransactionService>();
        TransactionRepository repository = new TransactionRepository(context, repoLogger);
        TransactionService service = new TransactionService(repository, serviceLogger);
        List<SignResult> results = await service.SignTransactionsAsync([transactionId], userId);
        return results.FirstOrDefault();
    }

    private static AppDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SimulationResults> QueryResultsAsync(string connectionString)
    {
        await using var context = CreateDbContext(connectionString);
        SimulationResults results = new SimulationResults();

        SiteSetting? setting = await context.SiteSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == "RequiredSignatures" || x.Key == "NumberOfRequiredAmSignatures");
        results.RequiredSignatures = int.TryParse(setting?.Value, out var r) ? r : 2;

        List<Transaction> transactions = await context.Transactions.AsNoTracking()
            .Where(t => t.Type != "Fee")
            .OrderBy(t => t.Id)
            .ToListAsync();

        foreach (var txn in transactions)
        {
            List<int> signers = await context.Signatures.AsNoTracking()
                .Where(s => s.TransactionId == txn.Id)
                .Select(s => s.UserId)
                .ToListAsync();

            results.Transactions.Add(new TransactionSummary
            {
                Id = txn.Id,
                Amount = txn.Amount,
                Status = txn.Status.ToString(),
                SignatureCount = signers.Count,
                UniqueSigners = signers.Distinct().Count(),
                FinalizationCount = await context.TransactionFinalizations.CountAsync(f => f.TransactionId == txn.Id),
                SettlementCount = await context.Settlements.CountAsync(s => s.TransactionId == txn.Id),
                FeeCount = await context.Transactions.CountAsync(t => t.Type == "Fee" && t.Reason == $"Fee for transaction {txn.Id}")
            });
        }

        results.TotalSignatures = await context.Signatures.CountAsync();
        results.TotalFinalizations = await context.TransactionFinalizations.CountAsync();
        results.TotalSettlements = await context.Settlements.CountAsync();
        results.TotalFees = await context.Transactions.CountAsync(t => t.Type == "Fee");

        return results;
    }

    private static void PrintSimulationSummary()
    {
        Console.WriteLine("Simulation Summary");
        Console.WriteLine($"- Attempts: {_totalAttempts}, Success: {_totalSuccesses}, Failed: {_totalFailures}");
        if (!ErrorCounts.IsEmpty)
        {
            Console.WriteLine("- Errors:");
            foreach (var e in ErrorCounts.OrderByDescending(x => x.Value))
                Console.WriteLine($"    {e.Key}: {e.Value}");
        }
        Console.WriteLine();
    }

    private static void PrintDatabaseResults(SimulationResults results)
    {
        Console.WriteLine("Database Summary");
        Console.WriteLine("  Id | Signs | Final | Settle | Fees | Status");
        foreach (var t in results.Transactions)
        {
            string warn = t.SignatureCount != t.UniqueSigners || t.FinalizationCount > 1 ? " !" : "";
            Console.WriteLine($"{t.Id,4} | {t.SignatureCount,4} | {t.FinalizationCount,5} | {t.SettlementCount,6} | {t.FeeCount,4} | {t.Status}{warn}");
        }
        Console.WriteLine($"Totals: Signs={results.TotalSignatures}, Finals={results.TotalFinalizations}, Settles={results.TotalSettlements}, Fees={results.TotalFees}");
        Console.WriteLine();
    }

    private static bool ValidateConsistency(SimulationResults results)
    {
        List<string> issues = new List<string>();

        foreach (var t in results.Transactions)
        {
            if (t.SignatureCount != t.UniqueSigners)
                issues.Add($"Tx {t.Id}: duplicate signatures");
            if (t.FinalizationCount > 1)
                issues.Add($"Tx {t.Id}: {t.FinalizationCount} finalizations");
            if (t.SettlementCount > 1)
                issues.Add($"Tx {t.Id}: {t.SettlementCount} settlements");
            if (t.FeeCount > 1)
                issues.Add($"Tx {t.Id}: {t.FeeCount} fees");
            if (t.FinalizationCount == 1 && (t.SettlementCount != 1 || t.FeeCount != 1))
                issues.Add($"Tx {t.Id}: finalized but missing settlement/fee");
            if (t.FinalizationCount == 1 && t.Status != "Completed")
                issues.Add($"Tx {t.Id}: finalized but status={t.Status}");
        }

        if (issues.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Validation: PASS");
            Console.ResetColor();
            return true;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Validation: FAIL");
        foreach (var i in issues) Console.WriteLine($"  - {i}");
        Console.ResetColor();
        return false;
    }
}
