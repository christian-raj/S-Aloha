namespace SAloha.Api.Core.Audit;

/// <summary>
/// Transition forcée (SOC-05) : un Admin peut passer outre le graphe des
/// transitions avec <c>?force=true&amp;reason=…</c> ; le motif est obligatoire et
/// tracé au journal d'audit. Les conditions du statut visé restent exigées.
/// </summary>
public static class ForcedTransition
{
    /// <param name="refusal">Refus du graphe, ou null si la transition est permise.</param>
    /// <returns>Erreur à renvoyer (400), ou Forbidden (403) pour un non-Admin qui force.</returns>
    public static (string? Error, bool Forbidden) Apply(HttpContext http, string? refusal)
    {
        if (refusal is null) return (null, false);
        if (http.Request.Query["force"] != "true") return (refusal, false);
        if (!http.User.IsInRole("Admin")) return (null, true);
        var reason = http.Request.Query["reason"].ToString().Trim();
        if (reason.Length == 0) return ("Motif obligatoire pour forcer une transition.", false);
        http.Items[AuditInterceptor.ForcedReasonKey] = reason;
        return (null, false);
    }
}
