using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransactionSign.Application.Interfaces;
using TransactionSign.Application.Services;
using TransactionSign.Infrastructure.Data;
using TransactionSign.Infrastructure.Repositories;

namespace TransactionSign.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool useInMemoryDb = false)
    {
        if (!useInMemoryDb)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(1),
                        errorNumbersToAdd: null)));
        }

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<TransactionService>();

        return services;
    }
}
