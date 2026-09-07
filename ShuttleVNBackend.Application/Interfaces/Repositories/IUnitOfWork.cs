namespace ShuttleVNBackend.Application.Interfaces.Repositories;

public interface IUnitOfWork
{
    Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}