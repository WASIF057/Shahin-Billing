using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Auth;

public class TokenService(IOptions<JwtSettings> options)
{
    private readonly JwtSettings _s = options.Value;

    private static List<Claim> Claims(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimsExtensions.UserIdClaim, user.Id),
            new("email", user.Email),
            new("name", user.Name),
            new("role", user.Role),
            new("tv", user.TokenVersion.ToString()),
            new(ClaimsExtensions.BusinessIdClaim, user.BusinessId),
        };
        // A client's login is tied to one client: the server always uses this, never anything the browser sends
        if (!string.IsNullOrEmpty(user.ClientId)) claims.Add(new Claim("clientId", user.ClientId));
        return claims;
    }

    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddHours(_s.ExpiryHours);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _s.Issuer,
            Audience = _s.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(Claims(user)),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.Key)), SecurityAlgorithms.HmacSha256)
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
