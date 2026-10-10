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
| R4 | Réouverture d'un problème sans motif : transitions (2026-10-10) et conditions de clôture (PRB-11 à PRB-14) en place ; reste le motif de réouverture ([PRB-10](reference/processus/gestion-des-problemes.md#4-cycle-de-vie)), avec les commentaires (SOC-24) | code |
| R6 | AD injoignable : 500 sur l'annuaire, « identifiants invalides » au login | code |

## 4. Chantiers à arbitrer

| # | Chantier | Décision à prendre |
|---|---|---|
| M3 | Actions confiées à un groupe AD invisibles de ses membres | Groupes dans le jeton, ou résolution à la volée dans l'AD |
| M7 | Pas d'édition d'une action (titre, échéance, RACI) | Périmètre de l'édition et droits |
| M8 | Aucune suppression dans l'interface | Qui supprime quoi, avec quelle confirmation |
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
| F11 | Incidents, sécurité | **Déclarations réglementaires** des incidents cyber (NIS 2 : alerte précoce 24 h, notification 72 h, rapport final ; RGPD : 72 h) associées à l'incident, échéances et alertes | [INC-40 à INC-44](reference/processus/gestion-des-incidents.md), [DRG-01 à DRG-13](reference/processus/declarations-reglementaires.md) |

Ces chantiers sont décidés : leurs règles sont écrites et classées en lots
([plan d'implémentation du lot 1](reference/processus/readme.md#plan-dimplémentation-conseillé--lot-1)).

**Itération suivante** (lot 1) : étape 4 (droits et statuts), étape 5 (groupes AD dans le
jeton), étape 6 (**périmètres**, [PER-01 à PER-12](reference/perimetres.md)) — décisions D1 à D4 acceptées
([ADR-0014](decisions/adr-0014-perimetres-droits-scopes.md)).

### Sortie du MVP

Une pratique perd la mention **(MVP)** — dans le README et la [cartographie](reference/produit.md#cartographie-des-processus-itil-et-état) —
quand les **prérequis communs** sont en place et que **toutes ses règles listées** ci-dessous
sont à ✅. La mention disparaît pratique par pratique, dans la PR qui passe la dernière
règle à ✅ (README, produit, CHANGELOG dans la même PR).

**Prérequis communs** : lot 1 terminé (étapes 4 à 6, périmètres compris) ;
[SOC-24](reference/regles-metier.md#9-traçabilité-commentaires-administration) commentaires ;
[SOC-21](reference/regles-metier.md#8-notifications) notifications.

| Pratique | Règles à ✅ (lot 1 restant, puis cœur de métier du lot 2) |
|---|---|
| [Incidents](reference/processus/gestion-des-incidents.md) | INC-11, INC-13 ; INC-07 (attente motivée, hors délais), INC-20 (service du catalogue), INC-21, INC-22 (échéances en heures de service), INC-24 (SLA en risque), INC-25 |
| [Demandes](reference/processus/gestion-des-demandes.md) | REQ-09 ; REQ-20 à REQ-23 (catalogue de demandes, modèles, échéance, groupe d'exécution), REQ-30 (retard), REQ-31 |
| [Problèmes](reference/processus/gestion-des-problemes.md) | PRB-10 (motif de réouverture), PRB-24, PRB-26 ; PRB-25 (approbation par l'A), PRB-27 (changement depuis une action), PRB-30 (article d'erreur connue), PRB-31 |
| [Changements](reference/processus/habilitation-des-changements.md) | CHG-14, CHG-15 (dates réelles, revue post-implémentation), CHG-16 (CI reliés), CHG-20, CHG-21 (autorité selon le risque, séparation des tâches), CHG-25 (gel), CHG-26 (modèles standard), CHG-24 |
| [Configuration](reference/processus/gestion-de-la-configuration.md) | CFG-12 à CFG-14 ; CFG-15 (vérification), CFG-20 (impact d'un changement), CFG-21 (CI ↔ service), CFG-22 |
| [Niveaux de service](reference/processus/gestion-des-niveaux-de-service.md) | SLM-04 (expiration), SLM-10, SLM-13, SLM-14 (heures de service), **SLM-20 (mesure du respect, F1)**, SLM-22, SLM-23, SLM-24 |
| [Connaissances](reference/processus/gestion-des-connaissances.md) | KB-10 (Markdown, [#25](https://github.com/christian-raj/S-Aloha/issues/25)) ; KB-11, KB-12, KB-17 (revue), KB-20, KB-21 (soumission à publication) |
| [Amélioration continue](reference/processus/amelioration-continue.md) | CSI-10, CSI-12 (source, priorisation), CSI-20 (proposition par le système), CSI-21, CSI-23 |
| [Conformité NIS 2](reference/processus/conformite-nis2.md) | NIS-18 ; NIS-08 (preuves), NIS-09 (comparaison), NIS-14, NIS-16 (revue périodique), NIS-17 (export) |

Les règles du lot 3 (enquêtes, versions d'articles, import en masse…) ne conditionnent pas la
sortie du MVP.

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
