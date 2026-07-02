namespace ProblemManagement.Api.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string DisplayName, string Role);
public record DirectoryEntry(string Id, string DisplayName, string Type); // Type: User | Group

public record RaciDto(string Role, string AssigneeType, string AssigneeId, string AssigneeDisplayName);

public record ActionDto(string Title, string Description, string? Status,
    DateTime? DueDate, List<RaciDto> Raci);

public record ProblemDto(string Title, string Description, string? Status, string Impact,
    string Urgency, string Category, string AffectedService,
    string? KnownErrorWorkaround, string? RootCause);

public record AnalysisDto(string Method, string DataJson, string? Conclusion);
