using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

public record RaciDto(string Role, string AssigneeType, string AssigneeId, string AssigneeDisplayName);

public record ActionDto(string Title, string Description, string? Status,
    DateTime? DueDate, List<RaciDto> Raci);

public record ProblemDto(string Title, string? Description, string? Status, string Impact,
    string Urgency, string Category, string AffectedService,
    string? KnownErrorWorkaround, string? RootCause, string? ClosureCode = null, int? PerimeterId = null,
    string? OwnerType = null, string? OwnerId = null, string? OwnerDisplayName = null,
    string? ReopenReason = null) : IRecordDto;

/// <summary>Changement qui met en œuvre le correctif d'une action (PRB-27).</summary>
public record ActionChangeDto(string Reference);

public record AnalysisDto(string Method, string DataJson, string? Conclusion);
