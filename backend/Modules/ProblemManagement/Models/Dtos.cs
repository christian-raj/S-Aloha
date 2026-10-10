namespace SAloha.Api.Modules.ProblemManagement;

public record RaciDto(string Role, string AssigneeType, string AssigneeId, string AssigneeDisplayName);

public record ActionDto(string Title, string Description, string? Status,
    DateTime? DueDate, List<RaciDto> Raci);

public record ProblemDto(string Title, string Description, string? Status, string Impact,
    string Urgency, string Category, string AffectedService,
    string? KnownErrorWorkaround, string? RootCause, string? ClosureCode = null);

public record AnalysisDto(string Method, string DataJson, string? Conclusion);
