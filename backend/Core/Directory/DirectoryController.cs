using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Auth;

namespace SAloha.Api.Core.Directory;

[ApiController]
[Route("api/directory")]
[Authorize(Policy = "User")]
public class DirectoryController(LdapService ldap) : ControllerBase
{
    /// <summary>Recherche utilisateurs et groupes AD (sélecteur RACI).</summary>
    [HttpGet("search")]
    public ActionResult<List<DirectoryEntry>> Search([FromQuery] string q)
        => string.IsNullOrWhiteSpace(q) || q.Length < 2
            ? new List<DirectoryEntry>()
            : ldap.Search(q.Trim());
}
