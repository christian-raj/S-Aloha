# 🗺️ Plan d'action

<sub>[← Documentation](readme.md)</sub>

> **Ce qu'il reste à faire.** Alimenté par les constats ([`archives/`](archives/readme.md)),
> vidé au fur et à mesure : un chantier terminé quitte ce plan et rejoint le journal du
> jour ([protocole](protocole-documentation.md)).
>
> Le statut de chaque bug vit dans [GitHub Issues](https://github.com/christian-raj/S-Aloha/issues)
> ([ADR-0001](decisions/adr-0001-github-issues-source-de-verite.md)) ; ce plan porte les
> **chantiers** et l'ordre de traitement.
>
> *Mis à jour le 2026-10-06 : [constat initial](archives/etat-initial-2026-10-06.md),
> [revue fonctionnelle](archives/revue-fonctionnelle-2026-10-06.md).*

**Qui** débloque : *code* (réalisable sans arbitrage), *décision* (à trancher d'abord),
*action* (geste d'exploitation hors du dépôt).

## 1. Bloquants fonctionnels — le processus est inutilisable

Aucun bloquant ouvert : B1 à B3 corrigés et couverts par des tests le 2026-10-06
([journal](archives/journal-2026-10-06.md)).

## 2. Défauts majeurs — correctifs locaux

M1, M2, M4, M5 et M6 corrigés et couverts par des tests le 2026-10-06
([journal](archives/journal-2026-10-06.md)). M3, M7 et M8 attendent une décision
(section 4).

## 3. Règles métier que l'API ne tient pas

[Règles métier](reference/regles-metier.md) décrit le comportement **voulu** : c'est le
code qui doit s'y conformer.

| # | Écart | Qui |
|---|---|---|
| R1 | Statut, impact, urgence, méthode, rôle RACI acceptés en texte libre | code |
| R2 | Règles RACI vérifiées à la création seulement, pas en modification | code |
| R3 | Date d'achèvement d'une action réécrite à chaque enregistrement | code |
| R4 | Cycle de vie non contraint (clôture avec actions ouvertes, Clos → Nouveau) | code : transitions **décidées** le 2026-10-07 ([PRB-10 à PRB-14](reference/processus/gestion-des-problemes.md#4-cycle-de-vie)) |
| R6 | AD injoignable : 500 sur l'annuaire, « identifiants invalides » au login | code |

## 4. Chantiers à arbitrer

| # | Chantier | Décision à prendre |
|---|---|---|
| M3 | Actions confiées à un groupe AD invisibles de ses membres | Groupes dans le jeton, ou résolution à la volée dans l'AD |
| M7 | Pas d'édition d'une action (titre, échéance, RACI) | Périmètre de l'édition et droits |
| M8 | Aucune suppression dans l'interface | Qui supprime quoi, avec quelle confirmation |
| R5 | Aucun historique des modifications | Journal d'audit : contenu et durée de conservation |
| A1 | **Console d'administration inexistante** (Admin = console Manager) | Décidé le 2026-10-07 : section de menu **Administration** (Admin : utilisateurs et rôles, santé, journal d'audit, nettoyage — [SOC-26](reference/regles-metier.md#9-traçabilité-commentaires-administration)) et section **Paramétrage** (Manager, Admin : référentiels — SOC-27 — et paramétrage des pratiques) ; voir [frontend § Navigation cible](reference/frontend.md#navigation-cible) |

## 5. Avant une mise en production

Détail : [sécurité § Points de durcissement](reference/securite.md#points-de-durcissement-avant-production).

| # | Chantier | Qui |
|---|---|---|
| S1 | **Secrets hors du dépôt** (`Jwt:Key`, mots de passe PostgreSQL et LDAP) | code + action |
| S2 | **LDAPS** et **HTTPS** | action |
| S3 | **CORS** restreint ; API et Swagger non publiés hors de nginx | code |
| S5 | Stockage du jeton côté navigateur (`sessionStorage` ou cookie `HttpOnly`) | décision |

## 6. Qualité

| # | Chantier | Qui |
|---|---|---|
| Q1 | **Tests** : socle en place (API : xUnit + Testcontainers ; frontend : Vitest), B1–B3 et M1, M2, M4–M6 couverts ; étendre aux scénarios R1–R6 | code |
| Q2 | **CI** : build backend et frontend, `python3 scripts/check-docs.py` | code |
| Q3 | Retirer `puppeteer-core` des dépendances du frontend (inutilisé, alourdit l'image) | décision |
| Q4 | Créer les issues GitHub des sections 1 à 7 et les lier ici | action |

## 7. Feuille de route fonctionnelle

Les sept processus sont livrés en **MVP** ([cartographie](reference/produit.md#cartographie-des-processus-itil-et-état),
[ADR-0007](decisions/adr-0007-pratiques-itil4-et-socle-commun-des-processus.md)). Leurs enrichissements
sont spécifiés règle par règle dans [`reference/processus/`](reference/processus/readme.md).

| # | Processus | Enrichissement | Règles |
|---|---|---|---|
| F1 | Niveaux de service | **Mesure de l'atteinte des SLA** : échéances sur l'incident, respect mesuré | [INC-20 à INC-22](reference/processus/gestion-des-incidents.md), [SLM-10, SLM-13, SLM-14, SLM-20](reference/processus/gestion-des-niveaux-de-service.md) |
| F2 | Incidents, Problèmes | Service et catégorie tirés du catalogue et d'un référentiel | [SOC-27](reference/regles-metier.md#9-traçabilité-commentaires-administration), [SLM-10](reference/processus/gestion-des-niveaux-de-service.md) |
| F3 | Demandes | Catalogue de demandes, approbation facultative, tâches | [REQ-20 à REQ-25](reference/processus/gestion-des-demandes.md) |
| F4 | Changements | Modèles standard, revue post-implémentation, gel, CAB | [CHG-15, CHG-20, CHG-25, CHG-26](reference/processus/habilitation-des-changements.md) |
| F5 | Configuration | Rattachement CI ↔ service, import en masse | [CFG-21, CFG-30](reference/processus/gestion-de-la-configuration.md) |
| F6 | Tous | Transitions contraintes, journal d'audit | [SOC-05, SOC-20](reference/regles-metier.md#5-règles-communes-aux-processus) et § 4 de chaque pratique |
| F7 | Tous | Notifications, commentaires, pièces jointes | [SOC-21 à SOC-25](reference/regles-metier.md#8-notifications) |
| F8 | Connaissances | Rendu Markdown, suggestion d'articles | [KB-10](reference/processus/gestion-des-connaissances.md), [INC-19](reference/processus/gestion-des-incidents.md) |

Ces chantiers sont décidés : leurs règles sont écrites et classées en lots
([plan d'implémentation du lot 1](reference/processus/readme.md#plan-dimplémentation-conseillé--lot-1)).

## 8. Communauté et financement

Accueil des contributeurs et financement du projet ; canaux et contreparties publics :
[Soutenir S-Aloha](soutenir.md).

| # | Chantier | Qui |
|---|---|---|
| C2 | Code de conduite (attend l'adresse de contact dédiée), `.github/FUNDING.yml` (après C6) | code + action |
| C5 | Tableau GitHub Projects public alimenté par ce plan (le jeton `gh` du mainteneur doit recevoir le droit `project` : `gh auth refresh -s project`) | action |
| C6 | **Activer GitHub Sponsors** (profil du mainteneur : informations bancaires et fiscales), niveaux et contreparties ([ADR-0011](decisions/adr-0011-financement-sponsoring-et-services.md)) | action |
| C7 | Lancement : LinuxFr, Reddit, AlternativeTo, Framalibre (textes prêts) ; téléverser l'image de partage `docs/screenshots/partage-1280x640.png` (Settings → Social preview) ; awesome-selfhosted **à partir du 2027-02-06** (première version de plus de 4 mois exigée) | action |
| C8 | Démo en ligne réinitialisée chaque nuit | décision |
| C9 | `GOVERNANCE.md` dès le deuxième contributeur régulier | décision |
