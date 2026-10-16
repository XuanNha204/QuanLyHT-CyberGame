using System.Security.Claims;
using CyberGame.Application.Common;

namespace CyberGame.Server.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public Guid? BranchId
    {
        get
        {
            var branchClaim = User?.FindFirst("branch_id")?.Value;
            return Guid.TryParse(branchClaim, out var id) ? id : null;
        }
    }

    public string? Username => User?.FindFirst(ClaimTypes.Name)?.Value ?? User?.FindFirst("unique_name")?.Value;

    public IReadOnlyList<string> Roles => User != null ? User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() : [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public string? ActorType => User?.FindFirst("actor_type")?.Value;

    public Guid? CustomerId
    {
        get
        {
            var custClaim = User?.FindFirst("customer_id")?.Value;
            return Guid.TryParse(custClaim, out var id) ? id : null;
        }
    }

    public string? MachineCode => User?.FindFirst("machine_code")?.Value;
}
