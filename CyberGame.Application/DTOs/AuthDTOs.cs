using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record LoginRequest(string Username, string Password, string? DeviceName = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserProfileDto User
);

public record RefreshTokenRequest(string RefreshToken, string? DeviceName = null);

public record UserProfileDto(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    string Username,
    string FullName,
    IReadOnlyList<string> Roles
);

public record UserDto(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    string Username,
    string FullName,
    bool IsActive,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Roles
);

public record CreateUserRequest(
    Guid? BranchId,
    string Username,
    string FullName,
    string Password,
    List<string> Roles
);

public record UpdateUserRequest(
    Guid? BranchId,
    string FullName,
    bool IsActive,
    List<string> Roles
);

public record ResetPasswordRequest(string NewPassword);
