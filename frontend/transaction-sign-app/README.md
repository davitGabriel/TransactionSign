# TransactionSign Frontend

Angular application for the multi-signature transaction approval system.

## Features

- View pending transactions requiring signatures
- Sign selected transactions with current user ID
- View completed (finalized) transactions
- Real-time updates via SignalR
- Toast notifications for signature and finalization events

## Prerequisites

- Node.js 18+
- Angular CLI 16+
- Backend API running on http://localhost:5000

## Quick Start

```powershell
# Install dependencies
npm install

# Start development server
ng serve
```

Open http://localhost:4200 in your browser.

## Project Structure

```
src/app/
+-- app.component.ts       # Main component with transaction lists
+-- app.component.html     # Template with tabs and tables
+-- app.component.css      # Styles
+-- app.module.ts          # Module configuration
+-- models/
|   +-- transaction.model.ts    # Transaction interfaces
+-- services/
    +-- transaction.service.ts  # HTTP API calls
    +-- signalr.service.ts      # Real-time updates
```

## Configuration

### API URL

Update the API base URL in `src/app/services/transaction.service.ts`:

```typescript
private readonly apiUrl = 'http://localhost:5000/api/transactions';
```

### SignalR Hub URL

Update the hub URL in `src/app/services/signalr.service.ts`:

```typescript
private readonly hubUrl = 'http://localhost:5000/hubs/transactions';
```

## SignalR Integration

The app receives real-time updates for:

### TransactionSigned Event

Fired when any user signs a transaction.

```typescript
interface TransactionSignedEvent {
  transactionId: number;
  signatureCount: number;
  requiredSignatures: number;
  timestamp: string;
}
```

**UI Behavior**: Updates signature count display. Shows toast when threshold reached.

### TransactionFinalized Event

Fired when a transaction reaches the signature threshold and is finalized.

```typescript
interface TransactionFinalizedEvent {
  transactionId: number;
  fee: number;
  settlement: number;
  timestamp: string;
}
```

**UI Behavior**: Removes transaction from pending list, shows success toast, refreshes completed list.

### Connection Handling

```typescript
.withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
```

SignalR automatically reconnects with exponential backoff.

## Testing Multiple Users

1. Open http://localhost:4200 in multiple browser windows
2. Set different User IDs in each window
3. Sign the same transaction from different windows
4. Observe real-time updates across all windows

## API Endpoints Used

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/transactions/pending?userId={id}` | Get pending transactions for user |
| GET | `/api/transactions/completed` | Get finalized transactions |
| POST | `/api/transactions/sign` | Sign selected transactions |
| POST | `/api/transactions/restore` | Reset all to pending state |

## Development

### Build

```powershell
ng build
```

Build artifacts are stored in `dist/`.

### Unit Tests

```powershell
ng test
```

### Lint

```powershell
ng lint
```

## Troubleshooting

### "Connection refused" in console

Backend API is not running. Start it:

```powershell
cd ../backend/TransactionSign.Api
dotnet run
```

### SignalR disconnects frequently

Check CORS configuration in backend `Program.cs`:

```csharp
policy.WithOrigins("http://localhost:4200")
      .AllowCredentials();  // Required for SignalR
```

### Transactions not updating

1. Check browser console for SignalR connection status
2. Verify hub URL matches backend configuration
3. Try refreshing the page to re-establish connection
