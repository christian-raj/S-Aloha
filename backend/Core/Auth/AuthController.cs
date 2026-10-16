using Microsoft.AspNetCore.Mvc;

namespace SAloha.Api.Core.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(LdapService ldap, TokenService tokens, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Identifiant et mot de passe requis." });

        (string Username, string DisplayName, string Role, IReadOnlyCollection<string> Groups)? result;
        try { result = await ldap.AuthenticateAsync(req.Username.Trim(), req.Password); }
        catch (DirectoryUnavailableException ex)
        {
            // R6 : un annuaire injoignable n'est pas une erreur d'identifiants.
            logger.LogError(ex, "Connexion impossible : annuaire injoignable");
            return StatusCode(503, new { message = "Annuaire injoignable : la connexion est impossible pour le moment. Réessayer dans quelques minutes." });
        }
        if (result is null)
            return Unauthorized(new { message = "Identifiants invalides ou compte hors des groupes autorisés." });

        var (account, display, role, groups) = result.Value;
        return new LoginResponse(tokens.Create(account, display, role, groups), account, display, role);
    }
}
