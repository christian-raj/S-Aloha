namespace SAloha.Api.Core.Auth;

/// <summary>Rôle applicatif et groupes AD directs d'un compte de l'annuaire.</summary>
public record DirectoryAccount(string Role, IReadOnlyCollection<string> Groups);

/// <summary>Annuaire des comptes, pour connaître le rôle d'un autre utilisateur que soi (INC-13).</summary>
public interface IUserDirectory
{
    /// <summary>Le compte, ou null s'il n'existe pas ou n'est dans aucun groupe autorisé ; lève une exception si l'annuaire est injoignable.</summary>
    Task<DirectoryAccount?> FindAsync(string username);
}
