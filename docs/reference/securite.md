# 🛡️ Sécurité

> Le modèle de sécurité tel qu'il est, et ce qui reste à durcir avant une mise en production. Correctifs suivis dans [GitHub Issues](https://github.com/christian-raj/S-Aloha/issues) (label `security`).

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## Identité et authentification

- Aucune identité locale : **aucun mot de passe ni compte n'est stocké** par S-Aloha.
- Connexion (`POST /api/auth/login`) : recherche de l'utilisateur dans l'AD par le compte
  de service, puis **bind LDAP avec le DN et le mot de passe de l'utilisateur** — c'est
  l'AD qui valide le mot de passe.
- Rôle déterminé par l'appartenance (`memberOf`) aux groupes configurés ; aucun groupe ⇒
  connexion refusée.
- Jeton **JWT HS256** signé avec `Jwt:Key`, vérifié sur l'issuer et la signature (pas
  d'audience), durée `Jwt:ExpiryHours` (8 h par défaut). Pas de révocation : un jeton
  reste valide jusqu'à expiration, même si l'utilisateur est retiré du groupe AD.

## Autorisation

Trois policies imbriquées dans `backend/Program.cs` :

| Policy | Rôles admis |
|---|---|
| `User` | User, Manager, Admin |
| `Manager` | Manager, Admin |
| `Admin` | Admin |

Toute route sauf `/api/auth/login` exige au minimum `User`. Le frontend masque des
actions selon le rôle, mais **le contrôle fait foi côté API**. La console est composée
côté serveur : un client ne reçoit jamais un bloc non autorisé pour son rôle. Matrice
complète : [`regles-metier.md`](regles-metier.md#1-rôles-et-droits).

## Surface exposée

| Port | Service | Contenu |
|---|---|---|
| 80 | `web` (nginx) | Frontend + proxy `/api/` |
| 8080 | `api` | API **et Swagger** (`/swagger`), exposés directement |
| — | `db` | Non publié hors du réseau compose |

## Points de durcissement avant production

État au 2026-10-06 — constat détaillé :
[`archives/etat-initial-2026-10-06.md`](../archives/etat-initial-2026-10-06.md).

| Point | État actuel | Cible |
|---|---|---|
| Secrets | `Jwt:Key`, mot de passe PostgreSQL et `BindPassword` en clair dans `appsettings.json` et `docker-compose.yml` | Secrets Docker ou variables d'environnement injectées, clé JWT aléatoire ≥ 64 caractères |
| LDAP | `UseSsl: false`, port 389 : mots de passe utilisateurs en clair sur le réseau | LDAPS (636) |
| Transport | HTTP sur le port 80 | HTTPS (reverse proxy TLS) |
| CORS | `AllowAnyOrigin` | Origine du frontend uniquement |
| Exposition | API et Swagger publiés sur 8080 | API joignable seulement via nginx ; Swagger désactivé en production |
| Jeton côté navigateur | `sessionStorage`, lisible par tout script de la page (XSS) | À arbitrer : cookie `HttpOnly` + protection CSRF |
| Révocation | Aucune avant expiration | Durée courte et/ou contrôle de l'appartenance AD |
| Tentatives de connexion | Aucune limitation de débit | Limitation par IP et par compte |

**Dépendances** — contrôle : `dotnet list package --vulnerable --include-transitive`
(backend) et `npm audit` (frontend). Corrigé le 2026-10-06 (`8f918a1`) :
`Microsoft.Extensions.Caching.Memory` 8.0.0, tirée par EF Core 8.0.8
([GHSA-qj66-m88j-hmgj](https://github.com/advisories/GHSA-qj66-m88j-hmgj), gravité haute) ;
passage à EF Core et JwtBearer 8.0.31, Npgsql EF 8.0.11. Plus aucune vulnérabilité
relevée sur l'API.

Frontend, corrigé le 2026-10-06 : 16 alertes, dont 2 critiques (tinypool, exécution de code
par pollution de prototype). Passage à Vite 6.4, Vitest 4.1 et react-router 7.18 ; les
dépendances transitives (postcss, esbuild, browserslist, source-map-js) suivent.
`npm audit` : 0 vulnérabilité.

## Contrôles du dépôt

Réglages du dépôt GitHub public, en place depuis le 2026-10-06 :

| Contrôle | Effet |
|---|---|
| Signalement privé de vulnérabilités | Les failles arrivent hors des issues publiques ([SECURITY.md](../../SECURITY.md)) |
| Secret scanning + push protection | Un push contenant un secret reconnu est refusé |
| Dependabot (alertes, correctifs, versions) | Alerte et PR de correctif dès qu'un avis touche une dépendance ; mises à jour hebdomadaires groupées (npm, NuGet, Docker, Actions) |
| CodeQL (configuration par défaut) | Analyse statique C# et JavaScript à chaque push et chaque semaine |
| CI ([architecture § Intégration continue](architecture.md#intégration-continue)) | Tests, build, audit des dépendances, santé de la doc |
| Règles de `main` | Ni suppression ni push forcé ; pour une pull request, CI au vert et historique linéaire |
| Actions | Seules les actions publiées par GitHub ; jeton en lecture seule ; workflows d'un contributeur externe soumis à approbation |

Non disponibles sans GitHub Advanced Security : détection des secrets hors fournisseurs
connus et vérification de validité des secrets détectés.
