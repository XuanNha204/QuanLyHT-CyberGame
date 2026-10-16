using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CyberGame.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CyberGame.Infrastructure.Security;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;
    private readonly int _memberExpiryMinutes;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
        _secret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("Cấu hình 'Jwt:Secret' bị thiếu.");
        if (_secret.Length < 32)
        {
            throw new InvalidOperationException("Cấu hình 'Jwt:Secret' phải có độ dài tối thiểu 32 ký tự.");
        }
        _issuer = _config["Jwt:Issuer"] ?? "CyberGameServer";
        _audience = _config["Jwt:Audience"] ?? "CyberGameClient";
        _expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out int exp) ? exp : 120;
        _memberExpiryMinutes = int.TryParse(_config["Jwt:MemberExpiryMinutes"], out int memExp) ? memExp : 720;
    }

    public string GenerateAccessToken(Guid userId, string username, Guid? branchId, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("actor_type", "staff"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (branchId.HasValue)
        {
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateMemberAccessToken(Guid customerId, string memberCode, Guid branchId, string machineCode)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customerId.ToString()),
            new(ClaimTypes.NameIdentifier, customerId.ToString()),
            new("customer_id", customerId.ToString()),
            new("branch_id", branchId.ToString()),
            new("machine_code", machineCode),
            new("actor_type", "member"),
            new(ClaimTypes.Role, "Member"),
            new(JwtRegisteredClaimNames.UniqueName, memberCode),
            new(ClaimTypes.Name, memberCode),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_memberExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, string Hash, DateTime ExpiresAtUtc) GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);
        string token = Convert.ToBase64String(randomBytes);
        string hash = HashToken(token);
        DateTime expires = DateTime.UtcNow.AddDays(7);
        return (token, hash, expires);
    }

    public string HashToken(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
