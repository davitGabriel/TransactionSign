# Load Tests (k6)

Simulates concurrent users signing transactions to validate concurrency handling.

## Prerequisites

- [k6](https://k6.io/docs/getting-started/installation/) installed
- API running locally
- SQL Server with database

## Quick Start

```powershell
# Quick test (5 minutes)
.\run-loadtest.ps1 -Quick

# Full test (2 hours)
.\run-loadtest.ps1

# Custom API URL
.\run-loadtest.ps1 -BaseUrl "http://localhost:5001" -Quick
```

## Direct k6 Run

```bash
# Quick mode
k6 run k6-workflow.js --env BASE_URL=http://localhost:5000 --env QUICK=1

# Full mode
k6 run k6-workflow.js --env BASE_URL=http://localhost:5000
```

## What It Tests

- 20-50 concurrent users signing transactions
- Automatic restore when all transactions are signed
- Validates no duplicate signatures, finalizations, or settlements

## Output

Results saved to `results/<timestamp>/`:
- `k6-output.log` - Full k6 output
- `k6-summary.json` - Metrics summary
- `db-validation.txt` - Database integrity check (PASS/FAIL)
