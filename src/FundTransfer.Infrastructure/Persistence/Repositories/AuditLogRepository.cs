using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;

namespace FundTransfer.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        await _context.AuditLog.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }
}
