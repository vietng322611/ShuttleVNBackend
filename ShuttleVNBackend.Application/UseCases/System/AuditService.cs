using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.System;
using ShuttleVNBackend.Application.Interfaces.Repositories.System;
using ShuttleVNBackend.Core.Entities.System;

namespace ShuttleVNBackend.Application.UseCases.System;

public class AuditService(IAuditRepository auditRepository)
{
    public async Task<PagedResult<Audit>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        return await auditRepository.GetAllAsync(page, ct);
    }

    public async Task<PagedResult<AuditDto>> SearchAsync(
        SearchAuditDto dto,
        CancellationToken ct = default)
    {
        return await auditRepository.SearchAsync(dto, ct);
    }
}