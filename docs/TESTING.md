## Test Types Overview

| Unit Tests | `Tests/Services/` | xUnit + NSubstitute | Test business logic in isolation |
| Integration Tests | `Tests/ConcurrencySimulation/` | Console app | Validate concurrency with real database |
| Load Tests | `Tests/LoadTests/` | k6 | Stress test under sustained load over network |

## Unit Tests

### Location

`backend/TransactionSign.Tests/Services/TransactionServiceTests.cs`

### What They Test

- Transaction eligibility validation
- Fee calculation (tiered rates, min/max clamping)
- Settlement calculation (5% of fee)
- Error handling (not found, already signed, not eligible)
- Finalization trigger conditions

## Integration Test (Concurrency Simulator)

### Location

`backend/TransactionSign.Tests/ConcurrencySimulation/`

### What It Tests

Validates database integrity under concurrent load:
- No duplicate signatures (same user, same transaction)
- Exactly one finalization per completed transaction
- Exactly one settlement per completed transaction
- No over-signing beyond required count

### Prerequisites

1. SQL Server running with configured database
2. `appsettings.json` with valid connection string

### Running Integration Tests

```powershell
cd backend/TransactionSign.Tests
dotnet run
```

### Configuration

Edit `appsettings.json` in the Tests project:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=TransactionSignDb;..."
  },
  "Simulation": {
    "MinConcurrentUsers": 20,
    "MaxConcurrentUsers": 50,
    "IterationsPerUser": 10,
    "MinDelayMs": 10,
    "MaxDelayMs": 100
  }
}
```

### Output

```
============================================================
Transaction Sign - Concurrency Simulation
============================================================

Simulation Configuration
- Users: 35 (range: 20-50)
- Iterations per user: 10
- Delay between actions: 10-100 ms

Press any key to start...
Completed in 2341 ms.

Simulation Summary
- Attempts: 350, Success: 8, Failed: 342
- Errors:
    Signature limit reached: 320
    Already signed: 22

Database Summary
  Id | Signs | Final | Settle | Fees | Status
   1 |    2 |     1 |      1 |    1 | Completed
   2 |    2 |     1 |      1 |    1 | Completed
   3 |    2 |     1 |      1 |    1 | Completed
   4 |    2 |     1 |      1 |    1 | Completed
Totals: Signs=8, Finals=4, Settles=4, Fees=4

Validation: PASS
RESULT: PASS
```

### Validation Checks

```csharp
foreach (var t in results.Transactions)
{
    if (t.SignatureCount != t.UniqueSigners)
        issues.Add($"Tx {t.Id}: duplicate signatures");
    if (t.FinalizationCount > 1)
        issues.Add($"Tx {t.Id}: {t.FinalizationCount} finalizations");
    if (t.SettlementCount > 1)
        issues.Add($"Tx {t.Id}: {t.SettlementCount} settlements");
}
```

## Load Tests (k6)

### Location

`backend/TransactionSign.Tests/LoadTests/`

### What It Tests

Extended stress testing with:
- Sustained concurrent users (20-50)
- Automatic transaction restoration when all signed
- Multi-hour test runs
- Database consistency validation

### Prerequisites

1. [k6](https://k6.io/docs/getting-started/installation/) installed
2. API running locally
3. Database configured

### Running Load Tests

```powershell
cd backend/TransactionSign.Tests/LoadTests

# Quick test (5 minutes)
.\run-loadtest.ps1 -Quick

# Full test (2 hours)
.\run-loadtest.ps1

# Custom API URL
.\run-loadtest.ps1 -BaseUrl "http://localhost:5001" -Quick
```

Or directly with k6:

```bash
# Quick mode
k6 run k6-workflow.js --env BASE_URL=http://localhost:5000 --env QUICK=1

# Full mode
k6 run k6-workflow.js --env BASE_URL=http://localhost:5000
```

### Output

Results saved to `results/<timestamp>/`:
- `k6-output.log` - Full k6 output
- `k6-summary.json` - Metrics summary
- `db-validation.txt` - Database integrity check

### Test Workflow

```javascript
// k6-workflow.js
export default function () {
    // 1. Get pending transactions
    const pending = http.get(`${BASE_URL}/api/transactions/pending?userId=${userId}`);

    // 2. Sign signable transactions
    const signable = pending.json().filter(t => t.canSign);
    if (signable.length > 0) {
        http.post(`${BASE_URL}/api/transactions/sign`, { ids: [signable[0].id] });
    }

    // 3. If all transactions completed, restore
    if (signable.length === 0) {
        http.post(`${BASE_URL}/api/transactions/restore`);
    }
}
```