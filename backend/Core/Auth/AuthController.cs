using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SAloha.Api.Core.Auth;

/// <summary>
/// Connexion par l'annuaire ; le jeton de session est posé dans un cookie HttpOnly (S5,
/// ADR-0018), jamais exposé aux scripts de la page.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthenticator directory, TokenService tokens, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Identifiant et mot de passe requis." });

        (string Username, string DisplayName, string Role, IReadOnlyCollection<string> Groups)? result;
        try { result = await directory.AuthenticateAsync(req.Username.Trim(), req.Password); }
        catch (DirectoryUnavailableException ex)
        {
            // R6 : un annuaire injoignable n'est pas une erreur d'identifiants.
            logger.LogError(ex, "Connexion impossible : annuaire injoignable");
            return StatusCode(503, new { message = "Annuaire injoignable : la connexion est impossible pour le moment. Réessayer dans quelques minutes." });
        }
        if (result is null)
            return Unauthorized(new { message = "Identifiants invalides ou compte hors des groupes autorisés." });

        var (account, display, role, groups) = result.Value;
        var token = tokens.Create(account, display, role, groups);
        Response.Cookies.Append(SessionCookie.Name, token, SessionCookie.Options(Request, DateTimeOffset.UtcNow.Add(tokens.Lifetime)));
        // Un client d'API (script) qui le demande reçoit aussi le jeton, pour l'en-tête Authorization.
        return new LoginResponse(req.ReturnToken ? token : null, account, display, role);
    }

    /// <summary>Utilisateur de la session en cours : l'interface se recharge sans rien stocker de secret.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "User")]
    public ActionResult<SessionUser> Me() => new SessionUser(User.Identity?.Name ?? "",
        User.FindFirst("displayName")?.Value ?? User.Identity?.Name ?? "",
        User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User");

    /// <summary>Déconnexion : le cookie de session est effacé.</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(SessionCookie.Name, SessionCookie.Options(Request, null));
        return NoContent();
    }
}
