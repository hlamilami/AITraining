using FundTransfer.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundTransfer.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AccountNumber).IsUnique();
            e.Property(a => a.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Transfer>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<AuditLogEntry>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ExchangeRate>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SourceCurrency, x.TargetCurrency, x.IsActive });
        });
    }
}
