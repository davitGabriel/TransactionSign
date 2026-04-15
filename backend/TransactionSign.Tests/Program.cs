using TransactionSign.Tests.ConcurrencySimulation;

// Entry point for running concurrency simulation as console app
// Run with: dotnet run --project TransactionSign.Tests
// Run tests with: dotnet test TransactionSign.Tests
return await ConcurrencySimulator.RunAsync(args);
