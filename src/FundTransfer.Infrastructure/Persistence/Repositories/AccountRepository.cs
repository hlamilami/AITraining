using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundTransfer.Infrastructure.Persistence.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
    {
        await _context.Accounts.AddAsync(account, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync(ct);
    }
}
