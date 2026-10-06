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
| R4 | Cycle de vie non contraint (clôture avec actions ouvertes, Clos → Nouveau) | **décision** : écrire les transitions permises dans les règles métier, puis coder |
| R6 | AD injoignable : 500 sur l'annuaire, « identifiants invalides » au login | code |

## 4. Chantiers à arbitrer

| # | Chantier | Décision à prendre |
|---|---|---|
| M3 | Actions confiées à un groupe AD invisibles de ses membres | Groupes dans le jeton, ou résolution à la volée dans l'AD |
| M7 | Pas d'édition d'une action (titre, échéance, RACI) | Périmètre de l'édition et droits |
| M8 | Aucune suppression dans l'interface | Qui supprime quoi, avec quelle confirmation |
| R5 | Aucun historique des modifications | Journal d'audit : contenu et durée de conservation |
| A1 | **Console d'administration inexistante** (Admin = console Manager) | Périmètre : accès et rôles, santé (base, AD), référentiels (catégories, services), journal d'audit, nettoyage — voir la [revue](archives/revue-fonctionnelle-2026-10-06.md#-console-dadministration) |

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
| Q6 | Captures d'écran : régénérer celles qui montrent la barre latérale (lettre de pilier retirée le 2026-10-06) | code |

## 7. Feuille de route fonctionnelle

Les sept processus sont livrés en **MVP** ([cartographie](reference/produit.md#cartographie-des-processus-itil-et-état),
[ADR-0007](decisions/adr-0007-pratiques-itil4-et-socle-commun-des-processus.md)). Enrichissements
envisagés, à prioriser ; chacun se cale sur le guide de la pratique ITIL 4 correspondante.

| # | Processus | Enrichissement | Qui |
|---|---|---|---|
| F1 | Niveaux de service | **Mesure de l'atteinte des SLA** : rapprocher la durée de résolution des incidents (priorité, service) des cibles P1–P4 ; échéance de résolution sur l'incident | décision : rattacher l'incident à un service du catalogue plutôt qu'au texte libre « Service affecté » |
| F2 | Incidents, Problèmes | Service affecté et catégorie tirés du catalogue et d'un référentiel, plutôt qu'en texte libre | décision (rejoint A1 : référentiels) |
| F3 | Demandes | Catalogue de demandes (modèles d'objets demandés, approbation facultative selon le modèle) | décision |
| F4 | Changements | Modèles de changements standard ; détection des conflits au calendrier (mêmes CI, même créneau) ; revue post-implémentation | code |
| F5 | Configuration | Vue d'impact (CI amont/aval d'un service) ; rattachement CI ↔ service ; import en masse | code |
| F6 | Tous | Transitions de statut contraintes (comme R4 pour les problèmes) et historique des modifications (R5) | décision |
| F7 | Tous | Pièces jointes, commentaires, notifications | décision |
| F8 | Connaissances | Rendu Markdown du contenu ; suggestion d'articles depuis un incident | code |

## 8. Communauté et financement

Découle de la [stratégie communauté et financement](strategie-communaute.md). Les
décisions D1 à D5 y sont détaillées avec une recommandation.

| # | Chantier | Qui |
|---|---|---|
| C1 | Trancher D1 (mode démo public), D2 (DCO ou CLA, **avant toute contribution extérieure**), D3 (langue), D4 (financement) | **décision** |
| C2 | Code de conduite, `.github/FUNDING.yml`, GitHub Discussions, résumé anglais du README | code + action |
| C3 | **Mode démonstration** : profil compose `demo` avec annuaire Samba AD, trois comptes, données d'exemple | code (après D1) |
| C4 | Version `v0.1.0` : notes de version, images publiées sur `ghcr.io` par la CI | code |
| C5 | 8 à 10 issues `good first issue` / `help wanted` rédigées depuis ce plan ; tableau GitHub Projects public | action |
| C6 | Activer GitHub Sponsors (profil du mainteneur), niveaux et contreparties | action (après D4) |
| C7 | Lancement : LinuxFr, Reddit, awesome-selfhosted, AlternativeTo, Framalibre ; image de partage du dépôt | action (après C3, C4) |
| C8 | Démo en ligne réinitialisée chaque nuit | décision (D5) |
| C9 | `GOVERNANCE.md` dès le deuxième contributeur régulier | décision |
