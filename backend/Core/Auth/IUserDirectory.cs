namespace SAloha.Api.Core.Auth;

/// <summary>Rôle applicatif et groupes AD directs d'un compte de l'annuaire.</summary>
public record DirectoryAccount(string Role, IReadOnlyCollection<string> Groups);

/// <summary>R6 : annuaire injoignable (serveur absent, connexion ou compte de service refusés).</summary>
public class DirectoryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Vérification d'un mot de passe dans l'annuaire, pour la connexion.</summary>
public interface IAuthenticator
{
    /// <summary>Le compte authentifié, ou null (identifiants invalides, compte hors des groupes) ; lève <see cref="DirectoryUnavailableException"/> si l'annuaire est injoignable.</summary>
    Task<(string Username, string DisplayName, string Role, IReadOnlyCollection<string> Groups)?> AuthenticateAsync(string username, string password);
}

/// <summary>Annuaire des comptes, pour connaître le rôle d'un autre utilisateur que soi (INC-13).</summary>
public interface IUserDirectory
{
    /// <summary>Le compte, ou null s'il n'existe pas ou n'est dans aucun groupe autorisé ; lève une exception si l'annuaire est injoignable.</summary>
    Task<DirectoryAccount?> FindAsync(string username);
}
