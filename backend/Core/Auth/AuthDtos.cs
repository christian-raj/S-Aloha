using System.Text.Json.Serialization;

namespace SAloha.Api.Core.Auth;

/// <summary><c>ReturnToken</c> : client d'API qui veut aussi le jeton dans la réponse (ADR-0018).</summary>
public record LoginRequest(string Username, string Password, bool ReturnToken = false);
public record LoginResponse([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token,
    string Username, string DisplayName, string Role);
public record SessionUser(string Username, string DisplayName, string Role);
