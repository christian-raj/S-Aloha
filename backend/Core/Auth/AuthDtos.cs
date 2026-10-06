namespace SAloha.Api.Core.Auth;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string DisplayName, string Role);
