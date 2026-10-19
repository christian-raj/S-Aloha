namespace SAloha.Api.Core.Auth;

/// <summary>
/// S5 (ADR-0018) : cookie de session HttpOnly, SameSite=Strict, limité à /api, Secure dès que
/// la requête arrive en HTTPS ; et protection CSRF : une requête qui modifie, authentifiée par
/// le cookie, porte l'en-tête <c>X-S-Aloha: 1</c>, qu'un site tiers ne peut pas ajouter.
/// L'en-tête Authorization (clients d'API) n'est pas concerné : un navigateur ne l'envoie
/// jamais de lui-même.
/// </summary>
public static class SessionCookie
{
    public const string Name = "saloha_session";
    public const string CsrfHeader = "X-S-Aloha";
    private const string FromCookie = "auth.from-cookie";

    public static CookieOptions Options(HttpRequest request, DateTimeOffset? expires) => new()
    {
        HttpOnly = true, Secure = request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/api",
        Expires = expires, IsEssential = true,
    };

    /// <summary>JwtBearer : sans en-tête Authorization, le jeton vient du cookie (et la requête le retient).</summary>
    public static Task ReadToken(Microsoft.AspNetCore.Authentication.JwtBearer.MessageReceivedContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.Request.Headers.Authorization) && ctx.Request.Cookies.TryGetValue(Name, out var token))
        {
            ctx.Token = token;
            ctx.HttpContext.Items[FromCookie] = true;
        }
        return Task.CompletedTask;
    }

    /// <summary>Après l'authentification : une modification portée par le cookie sans l'en-tête CSRF est refusée (403).</summary>
    public static async Task CsrfGuard(HttpContext ctx, Func<Task> next)
    {
        var method = ctx.Request.Method;
        var safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
        if (!safe && ctx.Items.ContainsKey(FromCookie) && ctx.Request.Headers[CsrfHeader] != "1")
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { message = "Requête refusée : en-tête de protection CSRF absent." });
            return;
        }
        await next();
    }
}
