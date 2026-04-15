# Concurrency Handling

## The Problem

In a multi-signature system, several race conditions can occur:

1. **Duplicate signature**: Same user's click sign twice
2. **Over-signing**: More signatures than required are recorded
3. **Double finalization**: Transaction finalized multiple times
4. **Lost update**: One signature overwrites another

### Example Race Condition

```
Time    User A                      User B
----    ------                      ------
T1      Read: 1 signature           Read: 1 signature
T2      Check: 1 < 2 required       Check: 1 < 2 required
T3      Insert signature            Insert signature
T4      Read: 2 signatures          Read: 3 signatures (!)
T5      Finalize                    Finalize again (!)
```

Without protection, both users could add signatures and both could trigger finalization.

## Solution Layers

TransactionSign uses multiple layers of protection:

```
+---------------------------------------------------+
|  Layer 1: Unique Constraints (Database)           |
|  - UNIQUE(TransactionId, UserId) on Signatures    |
|  - UNIQUE(TransactionId) on TransactionFinalization|
+---------------------------------------------------+
                      |
                      v
+---------------------------------------------------+
|  Layer 2: Pessimistic Locking (UPDLOCK+HOLDLOCK)  |
|  - Serializes signatures for SAME transaction     |
|  - Different transactions proceed in parallel     |
+---------------------------------------------------+
                      |
                      v
+---------------------------------------------------+
|  Layer 3: ReadCommitted Isolation                 |
|  - Prevents dirty reads                           |
|  - Lower contention than Serializable             |
+---------------------------------------------------+
                      |
                      v
+---------------------------------------------------+
|  Layer 4: EF Core Retry Strategy                  |
|  - Handles transient SQL errors                   |
|  - Automatic retry with exponential backoff       |
+---------------------------------------------------+
```

## Layer 1: Unique Constraints

Database constraints are the last line of defense and cannot be bypassed.

### Signature Uniqueness

```csharp
// AppDbContext.cs
modelBuilder.Entity<Signature>(e =>
{
    e.HasIndex(s => new { s.TransactionId, s.UserId }).IsUnique();
});
```

**Effect**: If user A signs transaction 1 twice (even simultaneously), only one INSERT succeeds. The second throws a constraint violation.

### Finalization Uniqueness

```csharp
modelBuilder.Entity<TransactionFinalization>(e =>
{
    e.HasIndex(f => f.TransactionId).IsUnique();
});
```

**Effect**: Even if two processes try to finalize the same transaction, only one INSERT succeeds.

## Layer 2: Pessimistic Locking

While unique constraints catch errors, we prefer to prevent them in the first place.

### The Locking Strategy

```csharp
// TransactionRepository.cs - AddSignatureAsync
int currentSignatureCount = await _context.Signatures
    .FromSqlRaw(
        "SELECT * FROM Signatures WITH (UPDLOCK, HOLDLOCK) WHERE TransactionId = {0}",
        sign.TransactionId)
    .CountAsync();
```

### What UPDLOCK + HOLDLOCK Does

| `UPDLOCK` | Acquires update lock, preventing other transactions from modifying or acquiring update/exclusive locks |
| `HOLDLOCK` | Holds the lock until transaction commits (equivalent to SERIALIZABLE for this query) |

### Why This Works

```
Time    User A                          User B
----    ------                          ------
T1      BEGIN TRANSACTION
T2      SELECT WITH (UPDLOCK,HOLDLOCK)  BEGIN TRANSACTION
        -> Acquires lock on TxId=1
T3      Count = 1                       SELECT WITH (UPDLOCK,HOLDLOCK)
                                        -> BLOCKED (waiting for lock)
T4      INSERT signature
T5      COMMIT
        -> Releases lock
T6                                      -> Unblocked, acquires lock
T7                                      Count = 2
T8                                      2 >= 2 required
                                        -> SignatureLimitReachedException
```

### Scope of Locking

The lock is **per-transaction**, not global:
- Signing Transaction 1 blocks other signatures for Transaction 1
- Signing Transaction 2 proceeds in parallel (different rows locked)

This provides correctness without serializing unrelated operations.

## Layer 3: ReadCommitted Isolation

```csharp
await using var dbTransaction = await _context.Database
    .BeginTransactionAsync(IsolationLevel.ReadCommitted);
```

### Why Not Serializable?

| **Serializable** | Prevents all anomalies | High lock contention, deadlocks, poor scaling |
| **ReadCommitted** | Good performance, no dirty reads | Requires explicit locking for specific scenarios |

ReadCommitted + explicit UPDLOCK/HOLDLOCK because:
1. Only signature counting needs strict serialization
2. Other reads (get pending, get completed) don't need it
3. Better performance under load

## Layer 4: EF Core Retry Strategy

```csharp
// DependencyInjection.cs
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(1),
            errorNumbersToAdd: null)));
```

### Handled Errors

The retry strategy handles transient SQL Server errors:
- Deadlocks (error 1205)
- Connection timeouts
- Transient network issues

### Integration with Transactions

```csharp
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    // Entire transaction block is retried if transient error occurs
    await using var dbTransaction = await _context.Database.BeginTransactionAsync();
    // ... operations ...
    await dbTransaction.CommitAsync();
});
```

## Race Condition Scenarios

### Scenario 1: Same User Double-Click

```
User A clicks "Sign" twice quickly for Transaction 1.

Request 1:
  T1: Acquire lock
  T2: Count = 1, insert signature
  T3: Commit

Request 2 (parallel):
  T1: Try to acquire lock -> BLOCKED
  T2: (waiting...)
  T3: Lock acquired, Count = 2
  T4: 2 >= 2 -> SignatureLimitReachedException
      OR
      Insert fails with UNIQUE constraint violation

Result: Only one signature recorded. Second request returns error.
```

### Scenario 2: Two Users Sign Simultaneously

```
User A and User B both sign Transaction 1 at the same time.

User A:
  T1: Acquire lock
  T2: Count = 1 < 2, insert signature (UserId=A)
  T3: Count = 2, finalize
  T4: Commit

User B (parallel):
  T1: Try to acquire lock -> BLOCKED
  T2: (waiting...)
  T3: Lock acquired, Count = 2
  T4: 2 >= 2 -> SignatureLimitReachedException

Result: Both signatures recorded correctly. Only one finalization.
```

### Scenario 3: User Signs Already-Signed Transaction

```
User A signed Transaction 1 earlier. User A tries to sign again.

T1: Service checks HasUserSignedAsync -> true
T2: Return error "Already signed" before even trying to insert

Result: Early rejection at application layer.
```

### Scenario 4: Two Users Trigger Finalization

```
Both User A and User B add the 2nd signature (race condition).

User A:
  T1: Insert signature
  T2: Count = 2, try to finalize
  T3: Insert into TransactionFinalizations

User B:
  T1: Insert signature -> UNIQUE violation (duplicate UserId)
  T2: Transaction rolled back

OR if different users:

User A:
  T1: Insert signature, count = 2, insert finalization
  T2: Commit

User B:
  T1: (blocked by lock until A commits)
  T2: Count = 2 >= 2 -> SignatureLimitReachedException

Result: one finalization due to UNIQUE constraint and locking.
```

## Error Handling

### Duplicate Signature Detection

```csharp
catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
{
    await dbTransaction.RollbackAsync();
    throw new InvalidOperationException(ErrorMessages.AlreadySigned);
}
```

### Signature Limit Detection

```csharp
if (currentSignatureCount >= requiredSignatures)
{
    throw new SignatureLimitReachedException(sign.TransactionId, requiredSignatures);
}
```

## Testing Concurrency

### Unit Tests

Mock repository returns simulate various scenarios:
- Already signed
- Signature limit reached
- Concurrent finalization

### Integration Tests (ConcurrencySimulator)

```powershell
dotnet run --project TransactionSign.Tests
```

Simulates 20-50 concurrent users signing transactions, then validates:
- No duplicate signatures
- Exactly one finalization per completed transaction
- All signature counts match unique signer counts

### Load Tests (k6)

E2E Test via network

```powershell
cd backend/TransactionSign.Tests/LoadTests
k6 run k6-workflow.js --env BASE_URL=http://localhost:5000 --env QUICK=1
```

Runs sustained load for extended periods, validating database consistency.

## Summary

| Same user signs twice | UNIQUE(TransactionId, UserId) constraint |
| More signatures than required | UPDLOCK+HOLDLOCK count check |
| Transaction finalized twice | UNIQUE(TransactionId) on finalization |
| Dirty reads | ReadCommitted isolation |
| Transient failures | EF Core retry strategy |

The layered approach ensures:
1. **Correctness**: Constraints guarantee data integrity
2. **Performance**: Locking is scoped to individual transactions
3. **Resilience**: Retries handle transient issues
