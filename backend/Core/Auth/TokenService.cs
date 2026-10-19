using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SAloha.Api.Core.Auth;

public class TokenService(IConfiguration config)
{
    /// <summary>Durée de validité du jeton (et du cookie de session).</summary>
    public TimeSpan Lifetime => TimeSpan.FromHours(double.Parse(config["Jwt:ExpiryHours"] ?? "8"));

    /// <summary>
    /// Jeton de session : compte, nom affiché, rôle et groupes AD (SOC-10 : un
    /// enregistrement confié à un groupe apparaît pour chacun de ses membres).
    /// </summary>
    public string Create(string username, string displayName, string role, IEnumerable<string>? groups = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"] ?? config["Jwt:Issuer"],   // S8
            claims: new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim("displayName", displayName),
                new Claim(ClaimTypes.Role, role)
            }.Concat((groups ?? []).Select(g => new Claim(Principals.GroupClaim, g))),
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
