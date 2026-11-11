using Microsoft.Extensions.Caching.Memory;
using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Caching;

public class MemoryLoanStatusCache(IMemoryCache cache) : ILoanStatusCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private static string Key(Guid id) => $"loan-status:{id}";

    public bool TryGet(Guid loanId, out LoanStatusDto? status) => cache.TryGetValue(Key(loanId), out status);

    public void Set(LoanStatusDto status) => cache.Set(Key(status.Id), status, Ttl);

    public void Remove(Guid loanId) => cache.Remove(Key(loanId));
}
