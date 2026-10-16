# Audit de code du 2026-10-16

<sub>[← Archives](readme.md)</sub>

Rapport d'audit reçu le 2026-10-16, analysé point par point contre le code de `main`
(cbdde89, après le passage à .NET 10). Pour chaque thème : ce que dit l'audit, ce que montre
le code, le verdict et la suite donnée. Verdicts : **confirmé** (le constat tient),
**partiel** (le constat tient en partie ou est déjà en partie traité), **déjà traité**,
**inexact**. Les suites ouvertes sont reportées au [plan d'action](../plan-action.md).

## Synthèse

| # | Thème | Gravité (audit) | Verdict | Suite |
|---|---|---|---|---|
| 1 | Secrets et configuration | Critique | Partiel | S1 (existant) précisé ; S7 : refus de démarrer avec les valeurs d'exemple hors développement |
| 2 | Authentification et JWT | Élevé | Confirmé | S8 : audience du jeton ; rotation de clé avec S1 |
| 3 | Annuaire LDAP | Moyen | Partiel | R7 : délai de connexion explicite ; pas de pool (charge faible) |
| 4 | Migrations et démarrage | Élevé | Confirmé | O1 : sondes de vie et de disponibilité, rattrapages hors du démarrage, migrations pilotables |
| 5 | Surface de l'API (entités exposées) | Moyen | Confirmé | A2 : chantier à arbitrer (DTO de réponse) ; test du contrat OpenAPI fait |
| 6 | Recherche hybride | Robuste | Partiel | Délai d'appel et repli déjà en place ; métriques avec O2 |
| 7 | Concurrence des services de fond | Moyen | Inexact | Structures déjà concurrentes ; aucune suite |
| 8 | Tests et qualité | Positif | Partiel | La CI exécute déjà les tests ; Q5 : mesure de couverture |
| 9 | Docker et déploiement | Moyen | Partiel | Q6 : `.dockerignore` ; le reste tient |
| 10 | Observabilité | Recommandé | Partiel | O2 : chantier à arbitrer ; analyse des dépendances et des secrets déjà active |

## 1. Secrets et configuration — partiel

- **Audit** : mots de passe PostgreSQL et LDAP, clé `Jwt:Key` en clair dans
  `appsettings.json`, `docker-compose.yml`, `docker-compose.demo.yml` ; scan de secrets en CI.
- **Code** : les valeurs de `appsettings.json` et `docker-compose.yml` sont des **exemples à
  remplacer** (`ChangeMe_Str0ng!`, `MotDePasseServiceAccount`, `CHANGEZ-CETTE-CLE…`), pas des
  secrets réels ; le point est déjà au plan (S1) et dans [sécurité](../reference/securite.md#points-de-durcissement-avant-production).
  Les mots de passe de `docker-compose.demo.yml` sont **publics par conception** (annuaire
  Samba de démonstration), et le fichier l'annonce en tête. GitHub analyse déjà les secrets du
  dépôt, avec **blocage à la poussée** (*secret scanning* et *push protection* actifs) : un
  outil supplémentaire en CI (gitleaks, trufflehog) ferait doublon.
- **Écart réel** : rien n'empêche de démarrer en production avec les valeurs d'exemple (clé
  JWT connue de tous : jetons falsifiables).
- **Suite** : S7 au plan — hors environnement de développement, l'API refuse de démarrer si
  `Jwt:Key`, `BindPassword` ou le mot de passe de base valent leur exemple ou si la clé fait
  moins de 64 caractères. S1 reste la cible (secrets injectés par l'orchestrateur ; sur l'AMD,
  secrets Kubernetes, déjà le cas). Aucune rotation à faire : aucun secret réel n'a été versionné.

## 2. Authentification et JWT — confirmé

- **Code** : `ValidateAudience = false` (`Program.cs`) ; clé symétrique HS256 lue en
  configuration ; jeton de 8 h sans révocation (déjà noté en sécurité).
- **Analyse** : l'API est seule émettrice et seule consommatrice de ses jetons ; sans audience,
  un jeton émis avec la même clé pour un autre service serait accepté. Le risque est faible tant
  que la clé est propre à S-Aloha (S1, S7), mais la vérification d'audience ne coûte rien. Une
  signature asymétrique ou JWKS n'a d'intérêt qu'avec un fournisseur d'identité externe : hors
  périmètre actuel (authentification par l'AD).
- **Suite** : S8 au plan — `Jwt:Audience`, émise et vérifiée. Durée courte et révocation
  restent au tableau de durcissement de [sécurité](../reference/securite.md).

## 3. Annuaire LDAP — partiel

- **Code** : `LdapService` singleton sans état ; une connexion par appel (connexion, liaison,
  recherche), libérée ensuite. Mot de passe du compte de service jamais journalisé ; identifiant
  saisi nettoyé avant journalisation. Depuis R6 (#116), un annuaire injoignable donne 503 et une
  erreur au journal ; les référents AD sont ignorés (INC-13).
- **Écart réel** : aucun délai de connexion explicite (`ConnectionTimeout` vaut 0 : délai TCP
  du système, jusqu'à plusieurs dizaines de secondes si le contrôleur ne répond pas).
- **Analyse** : les appels LDAP sont rares (connexion, sélecteur de personnes, désignation d'un
  responsable d'incident majeur) ; un pool ou un disjoncteur (Polly) serait prématuré.
- **Suite** : R7 au plan — `Ldap:TimeoutSeconds` (5 s par défaut) sur la connexion et les
  opérations. Métriques d'échec LDAP avec O2.

## 4. Migrations et démarrage — confirmé

- **Code** : `Program.cs` applique les migrations, fige les évaluations validées, aligne le
  référentiel NIS 2 et rattrape les échéances SLA **avant** d'écouter, de façon synchrone
  (`BackfillAsync(...).GetAwaiter().GetResult()`). Aucun point de santé HTTP.
- **Analyse** : choix délibéré pour une installation simple ([ADR-0008](../decisions/adr-0008-migrations-ef.md)) ;
  sur une grosse base, le démarrage s'allonge sans que l'orchestrateur sache pourquoi, et un
  rattrapage qui échoue empêche l'API de démarrer.
- **Suite** : O1 au plan — points `/health/live` et `/health/ready` (base joignable,
  migrations appliquées), rattrapage des échéances dans un service de fond, option
  `Database:MigrateOnStartup` (vrai par défaut) pour confier les migrations à une étape de
  déploiement.

## 5. Surface de l'API — confirmé

- **Code** : `RecordController` renvoie les entités EF ; `ReferenceHandler.IgnoreCycles`
  évite les cycles de navigation. Les champs internes sensibles sont déjà masqués
  (`[JsonIgnore]`, ex. `FrozenRequirementsJson`, navigation `Agreement` des revues), et certaines
  listes projettent déjà (registre des problèmes, des changements).
- **Analyse** : exposer les entités couple le contrat de l'API au schéma ; le risque principal
  est d'exposer un champ ajouté plus tard sans y penser. Passer à des DTO de réponse partout est
  un chantier transversal (dix pratiques).
- **Suite** : A2 au plan, à arbitrer (DTO de réponse par pratique, à l'occasion des
  évolutions). Un test du contrat OpenAPI existe depuis #115 (`OpenApiTests`) ; à étendre aux
  schémas des réponses si A2 est retenu.

## 6. Recherche hybride — partiel

- **Code** : appel au service d'embeddings borné par un délai (`CancelAfter`), repli sur la
  recherche plein texte si le service est absent, lent ou en erreur
  ([ADR-0013](../decisions/adr-0013-recherche-hybride-service-embeddings-separe.md)) ; textes
  découpés en passages de taille bornée (`Chunker`) ; état du service visible en
  Administration (RAG-08). Le service Ollama n'est publié que sur le réseau interne du compose.
- **Suite** : métriques (latence, erreurs) avec O2 ; rien d'autre.

## 7. Concurrence des services de fond — inexact

- **Code** : `IndexQueue` repose sur un `Channel` (sûr en concurrence), `VectorStore` sur un
  `ConcurrentDictionary`, l'intercepteur sur une `ConditionalWeakTable` par contexte. Pas
  d'état partagé non protégé relevé.
- **Suite** : aucune.

## 8. Tests et qualité — partiel

- **Code** : la CI (`ci.yml`) exécute déjà les tests d'API (Testcontainers, sur un runner avec
  Docker) et d'interface à chaque PR, avec CodeQL, `npm audit`, la vérification des dépendances
  vulnérables NuGet, les liens de la documentation, la construction des images et le DCO.
- **Suite** : Q5 au plan — mesure de la couverture (coverlet) publiée en CI, sans seuil
  bloquant au départ. `dotnet format` en CI : à joindre à Q5.

## 9. Docker et déploiement — partiel

- **Code** : Dockerfiles multi-étapes ; `COPY *.csproj` fonctionne (contexte `backend`, image
  construite à chaque PR en CI) ; publication des images par `publication.yml` ; le fichier de
  démonstration est explicitement marqué « jamais en production ».
- **Écart réel** : aucun `.dockerignore` (le contexte envoie `bin/`, `obj/`, `node_modules/`).
- **Suite** : Q6 au plan — `.dockerignore` pour `backend` et `frontend`.

## 10. Observabilité — partiel

- **Code** : journal ASP.NET Core standard ; Dependabot (mises à jour et alertes de sécurité),
  CodeQL et analyse des secrets déjà actifs. Pas de journal structuré, de traces ni de métriques.
- **Suite** : O2 au plan, à arbitrer — journal structuré (sortie JSON de la console) et
  OpenTelemetry (traces, métriques LDAP, embeddings, notifications), selon la plateforme
  d'exploitation retenue.
