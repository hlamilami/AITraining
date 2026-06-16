using FundTransfer.Application.Models;

namespace FundTransfer.Application.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);
}
