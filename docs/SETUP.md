## Prerequisites

| Software | Version | Required For |
|----------|---------|--------------|
| .NET SDK | 10.0+ | Backend API |
| Node.js | 18+ | Frontend app |
| SQL Server | 2019+ or LocalDB | Database |
| Angular CLI | 16+ | Frontend development |

### Optional Tools

| k6 | Load testing |
| SQL Server Management Studio | Database management |

## Quick Start

```powershell
# 1. Clone the repository
git clone <repository-url>
cd TransactionSign

# 2. Set up the database connection (see Database Setup below)

# 3. Start the backend
cd backend/TransactionSign.Api
dotnet run

# 4. Start the frontend (new terminal)
cd frontend/transaction-sign-app
npm install
ng serve
```

Open http://localhost:4200 in your browser.

## Database Setup

### Option A: SQL Server LocalDB (Recommended for Development)

LocalDB is included with Visual Studio and SQL Server Express.

1. The default connection string in `appsettings.json` uses LocalDB:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=TransactionSignDb;Trusted_Connection=true;TrustServerCertificate=true;"
     }
   }
   ```

2. The database will be created automatically on first run via EF Core migrations.

### Option B: SQL Server Instance

1. Create a database named `TransactionSignDb`

2. Update `backend/TransactionSign.Api/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=TransactionSignDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=true;"
     }
   }
   ```

### Database Migrations

Migrations run automatically when the API starts. To run manually:

```powershell
cd backend/TransactionSign.Infrastructure
dotnet ef database update --startup-project ../TransactionSign.Api
```

## Backend Setup

1. Navigate to the API project:
   ```powershell
   cd backend/TransactionSign.Api
   ```

2. Restore packages and run:
   ```powershell
   dotnet restore
   dotnet run
   ```

3. The API will be available at:
   - HTTP: http://localhost:5000
   - HTTPS: https://localhost:5001

### Verify Backend is Running

```powershell
# Get pending transactions (should return JSON array)
curl http://localhost:5000/api/transactions/pending?userId=1
```

## Frontend Setup

1. Navigate to the frontend project:
   ```powershell
   cd frontend/transaction-sign-app
   ```

2. Install dependencies:
   ```powershell
   npm install
   ```

3. Start the development server:
   ```powershell
   ng serve
   ```

4. Open http://localhost:4200

### Frontend Configuration

The frontend connects to the backend at `http://localhost:5000`. To change this, update:
- `src/app/services/transaction.service.ts` - API base URL
- `src/app/services/signalr.service.ts` - SignalR hub URL

## Project URLs Summary

| Frontend | http://localhost:4200 |
| Backend API | http://localhost:5000 |
| SignalR Hub | http://localhost:5000/hubs/transactions |
| OpenAPI Docs | http://localhost:5000/openapi/v1.json |

## Troubleshooting

### "Connection refused" when starting frontend

**Cause**: Backend API is not running.

**Solution**: Start the backend first:
```powershell
cd backend/TransactionSign.Api
dotnet run
```

### Database connection errors

**Cause**: SQL Server not running or connection string incorrect.

**Solutions**:
1. Verify SQL Server is running
2. Check the connection string in `appsettings.json`
3. Ensure the database exists or let EF Core create it

### SignalR connection issues

**Cause**: CORS policy blocking WebSocket connection.

**Solutions**:
1. Ensure backend is running on port 5000
2. Check that CORS is configured for `http://localhost:4200` in `Program.cs`
3. Try refreshing the frontend page

### Port already in use

**Cause**: Another process is using port 5000 or 4200.

**Solution**:
```powershell
# Find process using port
netstat -ano | findstr :5000

# Kill process by PID
taskkill /PID <pid> /F
```

### EF Core migration errors

**Cause**: Database schema out of sync.

**Solution**:
```powershell
cd backend/TransactionSign.Infrastructure
dotnet ef database drop --startup-project ../TransactionSign.Api
dotnet ef database update --startup-project ../TransactionSign.Api
```