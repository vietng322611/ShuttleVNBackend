using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.System;
using ShuttleVNBackend.Core.Entities.System;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.System;

public interface IAuditRepository
{
    public Task<PagedResult<Audit>> GetAllAsync(PageRequest page, CancellationToken ct = default);

    public Task<PagedResult<AuditDto>> SearchAsync(
        SearchAuditDto dto,
        CancellationToken ct = default);
}