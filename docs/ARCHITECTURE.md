# Architecture

TransactionSign follows Clean Architecture principles with a clear separation of concerns between layers.

## Layer Diagram

```
+---------------------------------------------------------------+
|                        Presentation                            |
|  TransactionSign.Api (Controllers, SignalR Hub, Program.cs)   |
+---------------------------------------------------------------+
                              |
                              v
+---------------------------------------------------------------+
|                        Application                             |
|  TransactionSign.Application (Services, DTOs, Interfaces)     |
+---------------------------------------------------------------+
                              |
                              v
+---------------------------------------------------------------+
|                          Domain                                |
|  TransactionSign.Domain (Entities, Enums, Value Objects)      |
+---------------------------------------------------------------+
                              ^
                              |
+---------------------------------------------------------------+
|                       Infrastructure                           |
|  TransactionSign.Infrastructure (EF Core, Repositories)       |
+---------------------------------------------------------------+
```

## Layer Responsibilities

### Domain Layer (`TransactionSign.Domain`)

The innermost layer containing enterprise business rules.

**Contents:**
- `Entities/` - Transaction, Signature, Settlement, etc.
- `Enums/` - TransactionStatus, InternalStatus

**Dependencies:** None (pure C# classes)

**Example:**
```csharp
public class Transaction
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public InternalStatus InternalStatus { get; set; }
    // Navigation properties
    public ICollection<Signature> Signs { get; set; }
}
```

### Application Layer (`TransactionSign.Application`)

Contains business logic and orchestrates the flow of data.

**Contents:**
- `Services/TransactionService.cs` - Core business logic
- `DTOs/` - Data transfer objects for API communication
- `Interfaces/` - Repository and notifier contracts
- `Exceptions/` - Domain-specific exceptions
- `Constants/` - Shared error messages

**Dependencies:** Domain

**Key Responsibilities:**
1. Validate signing eligibility
2. Calculate fees
3. Determine when finalization threshold is reached
4. Coordinate repository calls and notifications

**Example - Fee Calculation:**
```csharp
private static decimal CalculateFee(decimal amount)
{
    decimal rate = amount switch
    {
        < 10000 => 0.003m,
        < 50000 => 0.002m,
        _ => 0.001m
    };
    return Math.Clamp(amount * rate, 50, 1800);
}
```

### Infrastructure Layer (`TransactionSign.Infrastructure`)

Implements interfaces defined in Application layer.

**Contents:**
- `Data/AppDbContext.cs` - EF Core context with configurations
- `Repositories/TransactionRepository.cs` - Data access implementation
- `DependencyInjection.cs` - Service registration

**Dependencies:** Application, Domain, EF Core, SQL Server

**Key Responsibilities:**
1. Database configuration and migrations
2. Concurrency control (locking, isolation levels)
3. Retry strategies for transient failures
4. Unique constraint enforcement

### Presentation Layer (`TransactionSign.Api`)

Handles HTTP requests and real-time communication.

**Contents:**
- `Controllers/TransactionsController.cs` - REST API endpoints
- `Hubs/TransactionHub.cs` - SignalR hub
- `Services/TransactionNotifier.cs` - ITransactionNotifier implementation
- `Program.cs` - Application configuration

**Dependencies:** Application, Infrastructure

**Key Responsibilities:**
1. Route HTTP requests to appropriate services
2. Broadcast SignalR events to connected clients
3. Configure CORS, DI, middleware
4. Trigger database migrations on startup

## Data Flow

### Signing a Transaction

```
1. Angular App                    POST /api/transactions/sign
         |
         v
2. TransactionsController         Receives request, calls service
         |
         v
3. TransactionService             Validates eligibility, calls repository
         |
         v
4. TransactionRepository          Acquires lock, adds signature, commits
         |
         v
5. TransactionService             Checks threshold, finalizes if met
         |
         v
6. TransactionNotifier            Broadcasts via SignalR
         |
         v
7. Angular App                    Receives event, updates UI
```

### Real-Time Updates

```
SignalR Event Flow:

  TransactionService
         |
         | (calls after signature/finalization)
         v
  ITransactionNotifier
         |
         | (implementation)
         v
  TransactionNotifier
         |
         | (broadcasts to all clients)
         v
  TransactionHub
         |
         | (WebSocket)
         v
  Angular SignalRService
         |
         | (emits to component)
         v
  AppComponent (updates UI)
```

## Design Decisions

### Why Clean Architecture?

1. **Testability**: Business logic in Application layer can be unit tested with mocked repositories
2. **Flexibility**: Database could be swapped without changing business logic
3. **Maintainability**: Clear boundaries between concerns

### Why SignalR?

1. **Real-time updates**: Multiple users see signature changes immediately
2. **Automatic reconnection**: Built-in retry logic
3. **WebSocket + fallbacks**: Works across different network conditions

### Why EF Core with SQL Server?

1. **Locking hints**: SQL Server supports UPDLOCK, HOLDLOCK for fine-grained control
2. **Retry strategies**: Built-in support for transient failure handling
3. **Migrations**: Schema evolution without manual SQL scripts

### Why Not Serializable Isolation?

Serializable isolation would prevent all concurrency issues but at significant cost:
- High lock contention
- Deadlock potential
- Poor scalability

Instead, we use:
- ReadCommitted isolation (default)
- Explicit UPDLOCK + HOLDLOCK on specific queries
- Unique constraints as final safety net

See [CONCURRENCY.md](CONCURRENCY.md) for detailed explanation.

## Key Interfaces

### ITransactionRepository

```csharp
public interface ITransactionRepository
{
    Task<List<Transaction>> GetPendingTransactionsAsync();
    Task<Transaction?> GetByIdAsync(int id);
    Task<bool> HasUserSignedAsync(int transactionId, int userId);
    Task<int> GetSignatureCountAsync(int transactionId);
    Task<int> GetRequiredSignaturesAsync();
    Task AddSignatureAsync(Signature sign);
    Task<bool> TryFinalizeTransactionAsync(Transaction transaction, decimal fee, decimal settlement);
}
```

### ITransactionNotifier

```csharp
public interface ITransactionNotifier
{
    Task NotifyTransactionSignedAsync(int transactionId, int signatureCount, int requiredSignatures);
    Task NotifyTransactionFinalizedAsync(int transactionId, decimal fee, decimal settlement);
}
```

## Project References

```
TransactionSign.Api
  +-- TransactionSign.Application
  +-- TransactionSign.Infrastructure

TransactionSign.Infrastructure
  +-- TransactionSign.Application
  +-- TransactionSign.Domain

TransactionSign.Application
  +-- TransactionSign.Domain

TransactionSign.Domain
  (no project references)
```
