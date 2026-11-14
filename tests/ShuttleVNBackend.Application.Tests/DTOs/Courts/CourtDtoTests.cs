using ShuttleVNBackend.Application.DTOs.Courts;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.Courts.Enums;

namespace ShuttleVNBackend.Application.Tests.DTOs.Courts;

public class CourtDtoTests
{
    [Fact]
    public void FromEntity_ActiveCourt_KeepsNameAndStatus()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var court = new BadmintonCourt
        {
            CourtId = 7,
            Name = "Court 7",
            Description = "Indoor",
            Status = CourtStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddDays(1)
        };

        var dto = CourtDto.FromEntity(court, isInUse: true);

        Assert.Equal(7, dto.CourtId);
        Assert.Equal("Court 7", dto.Name);
        Assert.Equal("Indoor", dto.Description);
        Assert.Equal(nameof(CourtStatus.Active), dto.Status);
        Assert.True(dto.IsInUse);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(createdAt.AddDays(1), dto.UpdatedAt);
    }

    // A soft-deleted court has a null name (CK_Court_DeletedName), but the DTO
    // contract is non-nullable, so the mapping must substitute a placeholder.
    [Fact]
    public void FromEntity_DeletedCourtWithNullName_UsesPlaceholder()
    {
        var court = new BadmintonCourt
        {
            CourtId = 3,
            Name = null,
            Status = CourtStatus.Closed,
            DeletedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var dto = CourtDto.FromEntity(court, isInUse: false);

        Assert.Equal("DELETED", dto.Name);
        Assert.False(dto.IsInUse);
    }
}
