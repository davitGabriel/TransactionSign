using Microsoft.EntityFrameworkCore;
using TransactionSign.Domain.Entities;

namespace TransactionSign.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Signature> Signatures => Set<Signature>();
    public DbSet<TransactionFinalization> TransactionFinalizations => Set<TransactionFinalization>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Transaction
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.ParentId).HasColumnName("ParentId");
            e.Property(t => t.Type)
                .HasColumnName("Source")
                .HasConversion(
                    toProvider => string.Equals(toProvider, "Fee", StringComparison.OrdinalIgnoreCase) ? 5 : 4,
                    fromProvider => fromProvider == 5 ? "Fee" : "Credit")
                .HasColumnType("int");
            e.Property(t => t.Counterparty).HasColumnName("BeneficiaryName");
            e.Property(t => t.Company).HasColumnName("SenderName");
            e.Property(t => t.IsDebit).HasColumnName("IsDebit");
            e.Property(t => t.CreateDate).HasColumnName("CreateDate");
            e.Property(t => t.LastModifiedDate).HasColumnName("LastModifyDate");
            e.Property(t => t.CurrencyId).HasColumnName("CurrencyId").HasMaxLength(10);
            e.Property(t => t.Note).HasColumnName("Note");
            e.Property(t => t.AgentId).HasColumnName("AgentId");
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Reason).HasMaxLength(500);
            e.Property(t => t.RowVersion)
                .IsRowVersion();
        });

        // Signature - UNIQUE(TransactionId, UserId) for concurrency safety
        // If race condition occurs: Two concurrent requests pass the soft check in frontend, but only one INSERT succeeds from backend the other get DbUpdateException.
        modelBuilder.Entity<Signature>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.TransactionId, s.UserId }).IsUnique();  // Prevents duplicate user signatures
            e.HasOne(s => s.Transaction)                                    
                .WithMany(t => t.Signs)
                .HasForeignKey(s => s.TransactionId);
        });

        // TransactionFinalization - UNIQUE(TransactionId) for exactly-once finalization
        modelBuilder.Entity<TransactionFinalization>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.TransactionId).IsUnique();    // Ensures exactly-once finalization 
            e.HasOne(f => f.Transaction)
                .WithOne(t => t.Finalization)
                .HasForeignKey<TransactionFinalization>(f => f.TransactionId);
        });

        // Settlement
        modelBuilder.Entity<Settlement>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.TransactionId).IsUnique();  // Ensures exactly-one settlement per transaction
            e.Property(s => s.Amount).HasPrecision(18, 2);
            e.HasOne(s => s.Transaction)
                .WithMany()
                .HasForeignKey(s => s.TransactionId);
        });

        // SiteSetting
        modelBuilder.Entity<SiteSetting>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Key).HasColumnName("Name");
            e.HasIndex(s => s.Key).IsUnique();
        });

        // Seed data
        modelBuilder.Entity<SiteSetting>().HasData(
            new SiteSetting { Id = 1, Key = "NumberOfRequiredAmSignatures", Value = "2" }
        );

        // Seed sample transactions
        modelBuilder.Entity<Transaction>().HasData(
            new Transaction
            {
                Id = 1,
                ParentId = null,
                Type = "Credit",
                IsDebit = false,
                CreateDate = new DateTime(2024, 1, 15),
                ValueDate = new DateTime(2024, 1, 15),
                LastModifiedDate = new DateTime(2024, 1, 15),
                Reason = "Invoice #1001",
                Company = "ABC Corp",
                Counterparty = "XYZ Ltd",
                Amount = 5000.00m,
                CurrencyId = "EUR",
                Note = null,
                AgentId = null,
                Status = Domain.Enums.TransactionStatus.Internal,
                InternalStatus = Domain.Enums.InternalStatus.ToSign
            },
            new Transaction
            {
                Id = 2,
                ParentId = null,
                Type = "Credit",
                IsDebit = false,
                CreateDate = new DateTime(2024, 1, 16),
                ValueDate = new DateTime(2024, 1, 16),
                LastModifiedDate = new DateTime(2024, 1, 16),
                Reason = "Q1 Payment",
                Company = "ABC Corp",
                Counterparty = "Partner Inc",
                Amount = 25000.00m,
                CurrencyId = "EUR",
                Note = null,
                AgentId = null,
                Status = Domain.Enums.TransactionStatus.Internal,
                InternalStatus = Domain.Enums.InternalStatus.ToSign
            },
            new Transaction
            {
                Id = 3,
                ParentId = null,
                Type = "Credit",
                IsDebit = false,
                CreateDate = new DateTime(2024, 1, 17),
                ValueDate = new DateTime(2024, 1, 17),
                LastModifiedDate = new DateTime(2024, 1, 17),
                Reason = "Services",
                Company = "ABC Corp",
                Counterparty = "Vendor Co",
                Amount = 75000.00m,
                CurrencyId = "EUR",
                Note = null,
                AgentId = null,
                Status = Domain.Enums.TransactionStatus.Internal,
                InternalStatus = Domain.Enums.InternalStatus.ToSign
            },
            new Transaction
            {
                Id = 4,
                ParentId = null,
                Type = "Credit",
                IsDebit = false,
                CreateDate = new DateTime(2024, 1, 18),
                ValueDate = new DateTime(2024, 1, 18),
                LastModifiedDate = new DateTime(2024, 1, 18),
                Reason = "Refund",
                Company = "ABC Corp",
                Counterparty = "Customer A",
                Amount = 1500.00m,
                CurrencyId = "EUR",
                Note = null,
                AgentId = null,
                Status = Domain.Enums.TransactionStatus.Internal,
                InternalStatus = Domain.Enums.InternalStatus.ToSign
            }
        );
    }
}
