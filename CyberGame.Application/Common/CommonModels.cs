namespace CyberGame.Application.Common;

public static class PaginationHelper
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        int normPage = page < 1 ? 1 : page;
        int normPageSize = Math.Clamp(pageSize, 1, 200);
        return (normPage, normPageSize);
    }
}

public class PagedResult<T>
{
    private int _page = 1;
    private int _pageSize = 20;

    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 200);
    }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int count, int page, int pageSize)
    {
        var (normPage, normPageSize) = PaginationHelper.Normalize(page, pageSize);
        Items = items;
        TotalCount = count;
        Page = normPage;
        PageSize = normPageSize;
    }
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? BranchId { get; }
    string? Username { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    string? ActorType => null;
    Guid? CustomerId => null;
    string? MachineCode => null;
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string username, Guid? branchId, IEnumerable<string> roles);
    string GenerateMemberAccessToken(Guid customerId, string memberCode, Guid branchId, string machineCode);
    (string Token, string Hash, DateTime ExpiresAtUtc) GenerateRefreshToken();
    string HashToken(string token);
}
