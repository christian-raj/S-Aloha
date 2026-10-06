using Microsoft.AspNetCore.Mvc;

namespace SAloha.Api.Core.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(LdapService ldap, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Identifiant et mot de passe requis." });

        var result = ldap.Authenticate(req.Username.Trim(), req.Password);
        if (result is null)
            return Unauthorized(new { message = "Identifiants invalides ou compte hors des groupes autorisés." });

        var (account, display, role) = result.Value;
        return new LoginResponse(tokens.Create(account, display, role), account, display, role);
    }
}
