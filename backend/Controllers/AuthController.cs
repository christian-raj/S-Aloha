using Microsoft.AspNetCore.Mvc;
using ProblemManagement.Api.Models;
using ProblemManagement.Api.Services;

namespace ProblemManagement.Api.Controllers;

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

        var (display, role) = result.Value;
        return new LoginResponse(tokens.Create(req.Username, display, role), req.Username, display, role);
    }
}
