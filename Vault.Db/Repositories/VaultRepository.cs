using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Vault.Data.Context;
using Vault.Interfaces;

namespace Vault.Repositories;

public class VaultRepository<T> : IVaultRepository<T> where T : class
{

    protected readonly VaultContext _context;
    protected readonly DbSet<T> _dbset;

    public VaultRepository(VaultContext context)
    {
        _context = context;
        _dbset = _context.Set<T>();
    }
    public async Task AddAsync(T entity)
    {
        await _dbset.AddAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbset.AddRangeAsync(entities);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return  await _dbset.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbset.ToListAsync();
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        return await _dbset.FindAsync(id);
    }

    public void Remove(T entity)
    {
        _dbset.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbset.RemoveRange(entities);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }
}
