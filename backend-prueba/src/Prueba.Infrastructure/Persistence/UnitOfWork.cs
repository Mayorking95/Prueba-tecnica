using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Domain.Exceptions;

namespace Prueba.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("La solicitud fue modificada por otro usuario. Recarga e inténtalo de nuevo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new ConflictException("Ya existe un registro con esos datos.");
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await tx.CommitAsync(ct);
        return result;
    }
}
