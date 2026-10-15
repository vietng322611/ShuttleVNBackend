using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Application.DTOs.Users;

public record CustomerListItemDto(
    Guid CustomerId,
    string FullName,
    string Phone,
    string Email,
    Guid? AccountId,
    bool HasAccount,
    AccountStatus? AccountStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt);