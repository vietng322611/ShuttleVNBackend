using ShuttleVNBackend.Core.Entities.Courts;

namespace ShuttleVNBackend.Application.DTOs.Courts;

public record CourtDto(
    int CourtId,
    string Name,
    string Description,
    string Status,
    bool IsInUse,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CourtDto FromEntity(BadmintonCourt c, bool isInUse)
    {
        // Name is only null on soft-deleted courts (CK_Court_DeletedName),
        // and the DTO contract has no nullable name.
        return new CourtDto(
            c.CourtId, c.Name ?? "DELETED", c.Description, c.Status.ToString(),
            isInUse, c.CreatedAt, c.UpdatedAt);
    }
}