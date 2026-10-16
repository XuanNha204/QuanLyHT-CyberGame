using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken ct = default);
    Task<PagedResult<UserDto>> GetUsersAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly ICyberGameDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;

    public AuthService(
        ICyberGameDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUser)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _currentUser = currentUser;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalized = request.Username.Trim().ToUpperInvariant();
        var user = await _db.Users
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);

        if (user == null || !user.IsActive)
        {
            throw new DomainException("Tên đăng nhập hoặc mật khẩu không chính xác.", "invalid_credentials");
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            throw new DomainException($"Tài khoản bị tạm khóa đến {user.LockoutEndUtc.Value:HH:mm dd/MM/yyyy}.", "account_locked");
        }

        bool verified = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!verified)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5)
            {
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            }
            await _db.SaveChangesAsync(ct);
            throw new DomainException("Tên đăng nhập hoặc mật khẩu không chính xác.", "invalid_credentials");
        }

        // Reset failed login count
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;

        var roles = user.UserRoles.Select(r => r.Role.Code).ToList();
        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Username, user.BranchId, roles);
        var (refreshToken, hash, expires) = _jwtTokenService.GenerateRefreshToken();

        var tokenRecord = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAtUtc = expires,
            DeviceName = request.DeviceName,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(tokenRecord);
        await _db.SaveChangesAsync(ct);

        var profile = new UserProfileDto(
            user.Id,
            user.BranchId,
            user.Branch?.Name,
            user.Username,
            user.FullName,
            roles
        );

        return new LoginResponse(accessToken, refreshToken, expires, profile);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = _jwtTokenService.HashToken(request.RefreshToken);
        var existingToken = await _db.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.Branch)
            .Include(t => t.User.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existingToken == null || !existingToken.IsActive)
        {
            throw new DomainException("Refresh token không hợp lệ hoặc đã hết hạn.", "invalid_refresh_token");
        }

        var user = existingToken.User;
        if (!user.IsActive)
        {
            throw new DomainException("Tài khoản người dùng đã bị khóa.", "user_inactive");
        }

        // Revoke old token
        existingToken.RevokedAtUtc = DateTime.UtcNow;

        // Generate new pair
        var roles = user.UserRoles.Select(r => r.Role.Code).ToList();
        var newAccessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Username, user.BranchId, roles);
        var (newRefreshToken, newHash, newExpires) = _jwtTokenService.GenerateRefreshToken();

        var newTokenRecord = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAtUtc = newExpires,
            DeviceName = request.DeviceName,
            CreatedAtUtc = DateTime.UtcNow
        };

        existingToken.ReplacedByTokenId = newTokenRecord.Id;
        _db.RefreshTokens.Add(newTokenRecord);
        await _db.SaveChangesAsync(ct);

        var profile = new UserProfileDto(
            user.Id,
            user.BranchId,
            user.Branch?.Name,
            user.Username,
            user.FullName,
            roles
        );

        return new LoginResponse(newAccessToken, newRefreshToken, newExpires, profile);
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = _jwtTokenService.HashToken(refreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token != null && token.RevokedAtUtc == null)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new DomainException("Chưa xác thực người dùng.", "unauthenticated");
        var user = await _db.Users
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy thông tin người dùng.", "user_not_found");
        }

        return new UserProfileDto(
            user.Id,
            user.BranchId,
            user.Branch?.Name,
            user.Username,
            user.FullName,
            user.UserRoles.Select(r => r.Role.Code).ToList()
        );
    }

    private Guid? ValidateAndGetBranchScope()
    {
        if (_currentUser.BranchId.HasValue)
        {
            return _currentUser.BranchId.Value;
        }

        if (_currentUser.IsInRole("Manager"))
        {
            throw new DomainException("Tài khoản quản lý không có thông tin chi nhánh.", "branch_required");
        }

        return null;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (normPage, normPageSize) = PaginationHelper.Normalize(page, pageSize);
        var scopedBranchId = ValidateAndGetBranchScope();

        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (scopedBranchId.HasValue)
        {
            query = query.Where(u => u.BranchId == scopedBranchId.Value);
        }

        query = query.OrderBy(u => u.Username);

        int total = await query.CountAsync(ct);
        var items = await query.Skip((normPage - 1) * normPageSize).Take(normPageSize).ToListAsync(ct);

        var dtos = items.Select(u => new UserDto(
            u.Id,
            u.BranchId,
            u.Branch?.Name,
            u.Username,
            u.FullName,
            u.IsActive,
            u.LastLoginAtUtc,
            u.UserRoles.Select(r => r.Role.Code).ToList()
        )).ToList();

        return new PagedResult<UserDto>(dtos, total, normPage, normPageSize);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var scopedBranchId = ValidateAndGetBranchScope();
        var targetBranchId = request.BranchId;

        if (scopedBranchId.HasValue)
        {
            if (targetBranchId.HasValue && targetBranchId.Value != scopedBranchId.Value)
            {
                throw new DomainException("Không có quyền tạo người dùng cho chi nhánh khác.", "cross_branch_access");
            }
            targetBranchId = scopedBranchId.Value;
        }

        var normalized = request.Username.Trim().ToUpperInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedUsername == normalized, ct))
        {
            throw new ConflictException("Tên đăng nhập đã tồn tại trên hệ thống.", "username_exists");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = targetBranchId,
            Username = request.Username.Trim(),
            NormalizedUsername = normalized,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var allRoles = await _db.Roles.ToListAsync(ct);
        foreach (var roleCode in request.Roles)
        {
            var role = allRoles.FirstOrDefault(r => string.Equals(r.Code, roleCode, StringComparison.OrdinalIgnoreCase));
            if (role != null)
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new UserDto(
            user.Id,
            user.BranchId,
            null,
            user.Username,
            user.FullName,
            user.IsActive,
            null,
            request.Roles
        );
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var scopedBranchId = ValidateAndGetBranchScope();
        var query = _db.Users
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (scopedBranchId.HasValue)
        {
            query = query.Where(u => u.BranchId == scopedBranchId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.", "user_not_found");
        }

        if (scopedBranchId.HasValue && request.BranchId.HasValue && request.BranchId.Value != scopedBranchId.Value)
        {
            throw new DomainException("Không thể chuyển người dùng sang chi nhánh khác.", "cross_branch_access");
        }

        user.FullName = request.FullName.Trim();
        user.IsActive = request.IsActive;
        if (request.BranchId.HasValue && !scopedBranchId.HasValue)
        {
            user.BranchId = request.BranchId.Value;
        }
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Update roles
        user.UserRoles.Clear();
        var allRoles = await _db.Roles.ToListAsync(ct);
        foreach (var roleCode in request.Roles)
        {
            var role = allRoles.FirstOrDefault(r => string.Equals(r.Code, roleCode, StringComparison.OrdinalIgnoreCase));
            if (role != null)
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        await _db.SaveChangesAsync(ct);

        return new UserDto(
            user.Id,
            user.BranchId,
            user.Branch?.Name,
            user.Username,
            user.FullName,
            user.IsActive,
            user.LastLoginAtUtc,
            request.Roles
        );
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        var scopedBranchId = ValidateAndGetBranchScope();
        var query = _db.Users.AsQueryable();
        if (scopedBranchId.HasValue)
        {
            query = query.Where(u => u.BranchId == scopedBranchId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.", "user_not_found");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Revoke all refresh tokens
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens)
        {
            t.RevokedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
