<div align="center">

<img src="frontend/public/favicon.svg" width="72" alt="S-Aloha" />

# S-Aloha

### L'excellence du service IT au cœur de votre performance.

La plateforme ITIL qui se branche sur votre Active Directory<br/>
et montre à chacun **ce qui requiert son intervention, maintenant**.

![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![React 18](https://img.shields.io/badge/React-18-61DAFB?logo=react&logoColor=black)
![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![Docker Compose](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)
![Active Directory](https://img.shields.io/badge/Auth-Active%20Directory-0078D4)
[![Licence AGPLv3](https://img.shields.io/badge/Licence-AGPLv3-A42E2B)](LICENSE)

[Fonctionnalités](#pourquoi-s-aloha) · [Aperçu](#aperçu) · [Démarrer](#démarrer-en-2-minutes) · [Documentation](#documentation) · [Licence](#licence)

<br/>

<img src="docs/screenshots/02-console-admin.png" alt="Console S-Aloha" width="900" />

</div>

---

## Pourquoi S-Aloha

**🔐 Branché sur votre annuaire, dès le premier jour**
Vos utilisateurs se connectent avec leur compte Windows. Les droits suivent vos groupes
Active Directory : aucun compte à créer, aucun mot de passe stocké.

**🎯 Une console qui dit quoi faire**
Chaque utilisateur arrive sur ce qui l'attend, classé par criticité : actions en retard,
problèmes à qualifier, erreurs connues sans plan d'action. L'informatif passe après.

**🔍 L'analyse de cause racine, outillée**
Trois méthodes intégrées : **5 Pourquoi**, **Ishikawa 6M** et **arbre des défaillances**
avec portes ET/OU. Plusieurs analyses par problème, la conclusion alimente la cause racine.

**✅ Des actions qui aboutissent**
Chaque action corrective porte une échéance et une matrice **RACI** affectée à des
personnes ou des groupes de l'annuaire. Les retards remontent tout seuls.

**📊 Le pilotage sans export Excel**
MTTR, répartition par statut, priorité et catégorie, actions en retard avec leurs
responsables : tout est dans le Reporting.

**🏠 Chez vous, en une commande, et libre**
On-prem, trois containers, vos données restent dans votre SI. Logiciel libre sous
AGPLv3 : pas de licence par utilisateur, pas de dépendance à un éditeur.

## Une plateforme, tous vos processus ITIL

| Processus | |
|---|---|
| **Gestion des problèmes** — cause racine, erreurs connues, actions correctives | ✅ Disponible |
| Incidents · Demandes · Changements · Configuration (CMDB) | 🔜 Bientôt |
| Niveaux de service · Connaissances · Amélioration continue | 🔜 Bientôt |

Chaque processus s'ajoute comme un module, sur un socle commun : même connexion, même
console, même reporting.

## Aperçu

<table>
  <tr>
    <td width="50%"><img src="docs/screenshots/01-connexion.png" alt="Connexion" /><br/><b>Connexion</b> — avec le compte Active Directory</td>
    <td width="50%"><img src="docs/screenshots/03-problemes.png" alt="Registre des problèmes" /><br/><b>Registre des problèmes</b> — statut, priorité P1–P4 calculée</td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/04-probleme-informations.png" alt="Fiche problème" /><br/><b>Fiche problème</b> — impact × urgence, contournement, cause racine</td>
    <td><img src="docs/screenshots/05-analyse-5-pourquoi.png" alt="Analyse 5 Pourquoi" /><br/><b>Analyse de cause racine</b> — 5 Pourquoi, Ishikawa, FTA</td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/06-actions-raci.png" alt="Actions RACI" /><br/><b>Actions correctives</b> — échéance et matrice RACI</td>
    <td><img src="docs/screenshots/08-reporting.png" alt="Reporting" /><br/><b>Reporting</b> — MTTR, répartitions, retards</td>
  </tr>
</table>

## Démarrer en 2 minutes

```bash
git clone git@github.com:christian-raj/S-Aloha.git && cd S-Aloha
# Renseignez votre Active Directory dans docker-compose.yml (service api, variables Ldap__*)
docker compose up -d --build
```

Ouvrez **http://localhost** et connectez-vous avec un compte membre de l'un des groupes
`GRP-SALOHA-ADMINS`, `GRP-SALOHA-MANAGERS` ou `GRP-SALOHA-USERS` (noms configurables).

> [!IMPORTANT]
> La configuration fournie sert à l'évaluation : secrets d'exemple, ni LDAPS ni HTTPS.
> Avant une mise en production, suivez [la liste de durcissement](docs/reference/securite.md#points-de-durcissement-avant-production).

Configuration complète : [guide d'exploitation](docs/reference/exploitation.md).

## Sous le capot

| | |
|---|---|
| **Frontend** | React 18 + Vite, CSS natif à jetons, servi par nginx |
| **API** | ASP.NET Core 8 + Entity Framework Core, Swagger sur `/swagger` |
| **Données** | PostgreSQL 16 |
| **Identité** | Active Directory (LDAP) → JWT, rôles Admin / Manager / User |

Socle commun dans `backend/Core` et `frontend/src/core`, un module par processus dans
`Modules/` et `modules/`. Détail : [architecture](docs/reference/architecture.md).

## Documentation

| | |
|---|---|
| 🧭 [Produit](docs/reference/produit.md) | Vision, piliers S-A-L-O-H-A, feuille de route |
| 📐 [Règles métier](docs/reference/regles-metier.md) | Rôles, console, cycle de vie, priorité, RACI |
| 🏗️ [Architecture](docs/reference/architecture.md) | Containers, socle et modules, API |
| ⚙️ [Exploitation](docs/reference/exploitation.md) | Installation, Active Directory, mise à jour |
| 🛡️ [Sécurité](docs/reference/securite.md) | Modèle d'autorisation, durcissement |
| 📚 [Tout le reste](docs/readme.md) | Base de données, frontend, décisions, plan d'action |

## Licence

S-Aloha est un logiciel libre, distribué sous
[GNU Affero General Public License v3](LICENSE) ou toute version ultérieure.

Vous pouvez l'utiliser, le modifier et le redistribuer librement. Si vous mettez une
**version modifiée** à disposition d'utilisateurs à travers le réseau, vous devez leur
donner accès à son code source : l'interface affiche pour cela un lien « Code source »,
configurable ([détails](docs/reference/exploitation.md#licence-et-code-source)).

Copyright © 2026 Christian Rajaonary.

<div align="center">
<br/>
<sub><i>Service alohan'ny zavatra rehetra</i> — le service avant toute chose.</sub>
</div>
