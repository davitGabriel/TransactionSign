
## Seed Data

The database is seeded with sample transactions on first run:

```csharp
// AppDbContext.cs
modelBuilder.Entity<SiteSetting>().HasData(
    new SiteSetting { Id = 1, Key = "NumberOfRequiredAmSignatures", Value = "2" }
);

modelBuilder.Entity<Transaction>().HasData(
    new Transaction { Id = 1, Amount = 5000.00m, Reason = "Invoice #1001", ... },
    new Transaction { Id = 2, Amount = 25000.00m, Reason = "Q1 Payment", ... },
    new Transaction { Id = 3, Amount = 75000.00m, Reason = "Services", ... },
    new Transaction { Id = 4, Amount = 1500.00m, Reason = "Refund", ... }
);
```

## Migrations

### Applying Migrations

Migrations run automatically when the API starts:

```csharp
// Program.cs
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
db.Database.Migrate();
```

### Manual Migration Commands

```powershell
cd backend/TransactionSign.Infrastructure

# Create a new migration
dotnet ef migrations add MigrationName --startup-project ../TransactionSign.Api

# Apply migrations
dotnet ef database update --startup-project ../TransactionSign.Api

# Remove last migration (if not applied)
dotnet ef migrations remove --startup-project ../TransactionSign.Api

# Generate SQL script
dotnet ef migrations script --startup-project ../TransactionSign.Api -o script.sql
```

## Connection String

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=TransactionSignDb;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

## EF Core Configuration

### Retry Strategy

```csharp
options.UseSqlServer(
    connectionString,
    sqlOptions => sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 3,
        maxRetryDelay: TimeSpan.FromSeconds(1),
        errorNumbersToAdd: null));
```

### In-Memory Database (Testing)

```csharp
// For integration tests
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TestDb"));
```

## Data Flow

### Signing a Transaction

```sql
-- 1. Lock and count existing signatures
SELECT * FROM Signatures WITH (UPDLOCK, HOLDLOCK)
WHERE TransactionId = @txId

-- 2. Insert new signature
INSERT INTO Signatures (TransactionId, UserId, CreatedAt)
VALUES (@txId, @userId, @now)

-- 3. If threshold reached, finalize
INSERT INTO TransactionFinalizations (TransactionId, FinalizedAt)
VALUES (@txId, @now)

INSERT INTO Transactions (ParentId, Source, Amount, ...) -- Fee transaction
VALUES (@txId, 5, @fee, ...)

INSERT INTO Settlements (TransactionId, Amount, CreatedAt)
VALUES (@txId, @settlement, @now)

UPDATE Transactions
SET Status = 1, InternalStatus = 1, LastModifyDate = @now
WHERE Id = @txId
```

### Restoring All Transactions

```sql
DELETE FROM TransactionFinalizations
DELETE FROM Signatures
DELETE FROM Settlements
DELETE FROM Transactions WHERE Source = 5  -- Fees
UPDATE Transactions SET Status = 0, InternalStatus = 0
```

## Troubleshooting

### Constraint Violations

**Error**: `Cannot insert duplicate key row`

**Cause**: Concurrent request tried to insert duplicate signature or finalization.

**Handling**: Caught by application code, returns appropriate error message.

### Deadlocks

**Error**: `Transaction was deadlocked`

**Cause**: Two transactions waiting for each other's locks.

**Handling**: EF Core retry strategy automatically retries the operation.

### Connection Pool Exhaustion

**Symptoms**: Connection timeouts under heavy load.

**Solution**: Increase `Max Pool Size` in connection string or reduce concurrent operations.
