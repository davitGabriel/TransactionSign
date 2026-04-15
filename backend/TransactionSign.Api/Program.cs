using Scalar.AspNetCore;
using TransactionSign.Infrastructure;
using TransactionSign.Infrastructure.Data;
using TransactionSign.Application.Interfaces;
using TransactionSign.Api.Hubs;
using TransactionSign.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var isTesting = builder.Environment.IsEnvironment("Testing");
builder.Services.AddInfrastructure(builder.Configuration, useInMemoryDb: isTesting);

// Register SignalR notifier
builder.Services.AddScoped<ITransactionNotifier, TransactionNotifier>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});

var app = builder.Build();

// Auto-migrate and seed database (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TransactionSign API");
    });
}

app.UseCors("AllowAngular");
app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<TransactionHub>("/hubs/transactions");

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
