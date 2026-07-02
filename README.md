# Gestion des problèmes — ITIL v3

Application web de gestion du processus **Problem Management** (ITIL v3) : enregistrement des problèmes, analyse de cause racine (5 Pourquoi, Ishikawa 6M, arbre des défaillances FTA), actions correctives avec matrice **RACI** affectées à des utilisateurs ou groupes **Active Directory**, tableau de bord et reporting.

## Stack

| Couche    | Technologie                                   |
|-----------|-----------------------------------------------|
| Frontend  | React 18 + Vite, servi par nginx              |
| Backend   | ASP.NET Core 8 Web API + Entity Framework Core|
| Base      | PostgreSQL 16 (container)                     |
| Auth      | LDAP (AD on-prem) → JWT                       |
| Déploiement | Docker Compose (3 services : db, api, web)  |

> Pour passer sur SQL Server : remplacer le package `Npgsql.EntityFrameworkCore.PostgreSQL` par `Microsoft.EntityFrameworkCore.SqlServer`, `UseNpgsql` par `UseSqlServer` dans `Program.cs`, et le service `db` du compose par une image `mcr.microsoft.com/mssql/server`.

## Démarrage rapide

```bash
# 1. Adapter la configuration AD dans docker-compose.yml (section environment du service api)
# 2. Lancer
docker compose up -d --build
# 3. Ouvrir http://localhost  (API + Swagger : http://localhost:8080/swagger)
```

## Configuration Active Directory

Tout se paramètre dans `backend/appsettings.json` (ou via les variables d'environnement `Ldap__*` du compose, qui ont priorité) :

```json
"Ldap": {
  "Host": "dc01.mondomaine.local",
  "Port": 389,
  "UseSsl": false,
  "BaseDn": "DC=mondomaine,DC=local",
  "BindUser": "CN=svc-problemapp,OU=Services,DC=mondomaine,DC=local",
  "BindPassword": "***",
  "Groups": {
    "Admin":   "GRP-PROBLEM-ADMINS",
    "Manager": "GRP-PROBLEM-MANAGERS",
    "User":    "GRP-PROBLEM-USERS"
  }
}
```

- `BindUser` : compte de service utilisé pour rechercher les utilisateurs et groupes (lecture seule suffit).
- `Groups` : mapping **groupe AD → rôle applicatif**. Un utilisateur hors de ces groupes ne peut pas se connecter.
  - **Admin** : tous les droits, y compris suppression.
  - **Manager** (gestionnaire de problèmes) : modification des problèmes, statuts, erreurs connues, suppression d'analyses/actions.
  - **User** : déclaration de problèmes, participation aux analyses, création et mise à jour d'actions.
- En production, activez `UseSsl: true` avec le port 636 (LDAPS).

## Processus ITIL v3 couvert

1. **Détection et enregistrement** : déclaration avec impact × urgence → priorité P1–P4 calculée automatiquement, référence `PRB-AAAA-NNNN`.
2. **Investigation et diagnostic** : espace d'analyse par méthodologie au choix — chaîne des **5 Pourquoi**, diagramme d'**Ishikawa (6M)**, **arbre des défaillances (FTA)** avec portes ET/OU. Plusieurs analyses possibles par problème ; la conclusion alimente la cause racine.
3. **Erreur connue** : statut dédié + champ contournement documenté.
4. **Résolution** : actions correctives avec échéance et matrice **RACI** (au moins un R, exactement un A), chaque rôle affecté à un utilisateur ou groupe AD via recherche dans l'annuaire.
5. **Suivi et clôture** : vue transverse des actions (filtre « Mes affectations », retards signalés), tableau de bord avec problèmes ouverts, erreurs connues, actions en retard, MTTR, répartitions par statut/priorité/catégorie.

## Structure

```
├── docker-compose.yml
├── backend/            # ASP.NET Core 8
│   ├── Controllers/    # Auth, Problems, Analyses, Actions, Reports, Directory
│   ├── Services/       # LdapService (AD), TokenService (JWT)
│   ├── Models/         # Entités + DTOs
│   └── Data/           # AppDbContext (EF Core)
└── frontend/           # React + Vite
    └── src/
        ├── pages/      # Login, Dashboard, Problems, ProblemDetail, Actions
        └── components/ # FiveWhys, Ishikawa, FtaTree, RaciEditor, Layout
```

## Aperçu de l'application

Captures d'écran réalisées avec des données de démonstration.

### Connexion (Active Directory)
Authentification avec le compte AD ; l'accès est réservé aux membres des groupes paramétrés.

![Connexion](docs/screenshots/01-connexion.png)

### Ma console — personnalisée par rôle, orientée action
Page d'accueil composée côté serveur selon le rôle. Un bandeau totalise les éléments à traiter, puis la zone **À traiter** classe par criticité ce qui requiert une intervention (mes actions en retard, problèmes à qualifier, erreurs connues sans action, retards toutes équipes, mes actions en cours) ; la zone **À suivre** regroupe l'informatif. La volumétrie a été déplacée vers le Reporting. Ici la vue **Admin**.

![Console](docs/screenshots/02-console-admin.png)

### Problèmes — enregistrement et suivi
Liste filtrable (statut, recherche) avec badges de statut et de priorité P1–P4 calculée.

![Problèmes](docs/screenshots/03-problemes.png)

### Fiche problème — qualification
Impact × urgence, catégorie, contournement (erreur connue) et cause racine validée. Modification réservée aux gestionnaires.

![Fiche problème](docs/screenshots/04-probleme-informations.png)

### Analyse de cause racine
Espace d'analyse par méthodologie au choix : **5 Pourquoi** (ci-dessous), **Ishikawa 6M** ou **arbre des défaillances FTA** avec portes ET/OU. Plusieurs analyses possibles par problème.

![Analyse 5 Pourquoi](docs/screenshots/05-analyse-5-pourquoi.png)

### Actions correctives — matrice RACI
Chaque action porte une échéance et des affectations RACI vers des utilisateurs ou groupes AD (au moins un R, exactement un A).

![Actions RACI](docs/screenshots/06-actions-raci.png)

### Suivi transverse des actions
Vue globale filtrable par statut ou « Mes affectations », avec signalement des retards.

![Suivi des actions](docs/screenshots/07-suivi-actions.png)

### Reporting
Indicateurs du processus : volumétrie globale (problèmes, actions, analyses, déclarants), répartitions par statut / priorité / catégorie, actions en retard, MTTR.

![Reporting](docs/screenshots/08-reporting.png)

## Documentation

- [`docs/rules.md`](docs/rules.md) — règles de gestion : rôles et droits, consoles par rôle, cycle de vie, matrice de priorité, règles RACI.
- [`docs/architecture.md`](docs/architecture.md) — architecture technique : containers, auth AD/JWT, modèle de données, API, frontend, déploiement.

## Points de durcissement avant production

- Changer `Jwt:Key` et le mot de passe PostgreSQL (utiliser des secrets Docker ou variables d'environnement).
- Activer LDAPS et restreindre CORS.
- Le schéma est créé automatiquement (`EnsureCreated`) ; pour gérer les évolutions, passer aux migrations EF (`dotnet ef migrations add`).
