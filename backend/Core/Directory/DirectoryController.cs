using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Auth;

namespace SAloha.Api.Core.Directory;

[ApiController]
[Route("api/directory")]
[Authorize(Policy = "User")]
public class DirectoryController(LdapService ldap, ILogger<DirectoryController> logger) : ControllerBase
{
    /// <summary>Recherche utilisateurs et groupes AD (sélecteur RACI).</summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<DirectoryEntry>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return new List<DirectoryEntry>();
        try { return await ldap.SearchAsync(q.Trim()); }
        catch (DirectoryUnavailableException ex)
        {
            // R6 : 503 explicite plutôt qu'une erreur 500.
            logger.LogError(ex, "Recherche impossible : annuaire injoignable");
            return StatusCode(503, new { message = "Annuaire injoignable : la recherche de personnes et de groupes est impossible pour le moment." });
        }
    }
}
