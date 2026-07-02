# Architecture technique

## 1. Vue d'ensemble

```
┌─────────────┐   HTTPS    ┌──────────────────┐   TCP 5432   ┌──────────────┐
│  Navigateur │ ─────────▶ │  web (nginx)     │              │  db          │
│  (React)    │            │  - fichiers React│              │  PostgreSQL16│
└─────────────┘            │  - proxy /api/ ──┼──────┐       └──────▲───────┘
                           └──────────────────┘      │              │ EF Core
                                                     ▼              │
                                            ┌──────────────────┐    │
                            LDAP(S) 389/636 │  api (.NET 8)    │────┘
                           ┌──────────────◀─┤  ASP.NET Core    │
                           │                └──────────────────┘
                    ┌──────┴───────┐
                    │  AD on-prem  │
                    └──────────────┘
```

Trois containers orchestrés par `docker-compose.yml` : **web** (nginx sert le build React et proxifie `/api/` vers l'API), **api** (ASP.NET Core 8), **db** (PostgreSQL 16, volume persistant `pgdata`).

## 2. Stack

| Couche | Choix | Notes |
|---|---|---|
| Frontend | React 18 + Vite, react-router | Build statique servi par nginx ; aucune dépendance UI lourde (CSS maison) |
| Backend | ASP.NET Core 8 Web API | Controllers REST, Swagger exposé sur `/swagger` |
| ORM | Entity Framework Core 8 | Provider Npgsql ; schéma créé par `EnsureCreated()` au démarrage |
| Base | PostgreSQL 16 | Remplaçable par SQL Server (provider EF + image du compose) |
| Annuaire | Novell.Directory.Ldap.NETStandard | Bind de service pour la recherche, bind utilisateur pour l'authentification |
| Auth API | JWT Bearer (HS256) | Claims : name, displayName, role ; expiration paramétrable |

## 3. Authentification et autorisation

1. `POST /api/auth/login` : l'API se connecte à l'AD avec le **compte de service** (`Ldap:BindUser`), recherche l'utilisateur (`Ldap:UserFilter`), puis valide le mot de passe par un **bind avec le DN de l'utilisateur**.
2. Les groupes `memberOf` sont comparés au mapping `Ldap:Groups` (Admin > Manager > User). Aucun groupe correspondant ⇒ connexion refusée.
3. Un **JWT** est émis avec le rôle en claim. Le frontend le stocke en `sessionStorage` et l'envoie en `Authorization: Bearer`.
4. Côté API, trois policies imbriquées : `User` (tous les rôles), `Manager` (Manager + Admin), `Admin`.
5. La console (`GET /api/console`) compose sa réponse **côté serveur** selon le rôle du jeton ; le frontend n'y reçoit que les blocs autorisés, classés pour mettre l'actionnable en premier.

En production : activer LDAPS (`Ldap:UseSsl=true`, port 636), servir le frontend en HTTPS, externaliser `Jwt:Key` et les mots de passe (secrets Docker / variables d'environnement).

## 4. Modèle de données

```
Problem 1──∞ RcaAnalysis
   │
   1──∞ CorrectiveAction 1──∞ RaciAssignment
```

- **Problem** : référence unique `PRB-AAAA-NNNN`, statut, impact/urgence/priorité, catégorie, service affecté, contournement (erreur connue), cause racine validée, horodatages (création, mise à jour, clôture).
- **RcaAnalysis** : méthode (`FIVE_WHYS | ISHIKAWA | FTA`), données au format **JSON** (`DataJson`) propre à chaque méthode, conclusion. Le JSON permet d'ajouter une méthodologie sans migration de schéma.
- **CorrectiveAction** : titre, description, échéance, statut, horodatage de complétion.
- **RaciAssignment** : rôle (`R|A|C|I`), type d'affecté (`User|Group`), identifiant AD (`sAMAccountName` ou `cn`) et nom d'affichage.

Suppressions en cascade : Problem → Analyses/Actions → RaciAssignments.

### Formats JSON des analyses
- 5 Pourquoi : `{ "problem": "...", "whys": ["...", ...], "rootCause": "..." }`
- Ishikawa : `{ "categories": { "Méthode": ["..."], ... }, "rootCause": "..." }`
- FTA : arbre récursif `{ "root": { "label": "...", "gate": "OR|AND", "children": [...] }, "rootCause": "..." }`

## 5. API (résumé)

| Méthode / Route | Policy | Rôle métier |
|---|---|---|
| POST `/api/auth/login` | — | Authentification AD → JWT |
| GET `/api/console` | User | Console par rôle, orientée action (blocs « à traiter » / « à suivre ») |
| GET/POST `/api/problems`, GET `/api/problems/{id}` | User | Liste (filtres statut, texte), détail, déclaration |
| PUT `/api/problems/{id}` | Manager | Qualification, statuts, cause racine, contournement |
| DELETE `/api/problems/{id}` | Admin | Suppression |
| GET/POST/PUT `/api/problems/{id}/analyses[...]` | User | Analyses RCA (DELETE : Manager) |
| GET `/api/actions`, POST `/api/problems/{id}/actions`, PUT `/api/actions/{id}` | User | Actions + RACI (DELETE : Manager) |
| GET `/api/directory/search?q=` | User | Recherche utilisateurs/groupes AD (sélecteur RACI) |
| GET `/api/reports/summary` | User | Indicateurs globaux : statuts, priorités, catégories, retards, MTTR + volumétrie (totaux, analyses, déclarants, dernière activité) |

## 6. Frontend

```
src/
├── api.js              # client fetch + gestion JWT (401 ⇒ retour login)
├── App.jsx             # routes : / (Console), /problems, /problems/:id, /actions, /reports
├── components/         # Layout (navigation), RaciEditor, FiveWhys, Ishikawa, FtaTree
└── pages/              # Login, Console, Problems, ProblemDetail (onglets), Actions, Dashboard (reporting)
```

Principes UI : fond neutre très clair (`#f7f8f9`), accents bleus (marine `#0d3a5c`, bleu `#1d6fa5`), typographie Manrope, badges de statut/priorité/RACI, tableaux cliquables, responsive mobile (< 900 px).

## 7. Build et déploiement

- `docker compose up -d --build` : build multi-étapes (SDK .NET → runtime ASP.NET ; node → nginx).
- Frontend exposé sur **:80**, API sur **:8080** (Swagger : `/swagger`).
- La configuration AD/JWT/BDD est surchargée par les variables d'environnement du compose (`Ldap__*`, `Jwt__*`, `ConnectionStrings__Default`).
- Évolutions de schéma : passer de `EnsureCreated()` aux migrations EF (`dotnet ef migrations add`).
