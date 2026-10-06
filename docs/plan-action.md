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

Détail et preuves : [revue fonctionnelle § Bloquants](archives/revue-fonctionnelle-2026-10-06.md#-bloquants).

| # | Défaut | Qui |
|---|---|---|
| B1 | Impossible de créer une analyse depuis l'interface (onglet qui plante) | code |
| B2 | Après la suppression d'un problème, plus aucune déclaration possible (référence en double → 500) | code |
| B3 | Déclarations simultanées : une seule réussit, les autres → 500 | code |

À fermer chacun avec un test (B2 et B3 : référence tirée d'une séquence PostgreSQL).

## 2. Défauts majeurs — correctifs locaux

| # | Défaut | Qui |
|---|---|---|
| M1 | Arbre des défaillances : la saisie perd le focus à chaque caractère | code |
| M2 | « Mes actions » dépend de la casse de l'identifiant saisi au login | code |
| M4 | Réouverture d'un problème clos : `ClosedAt` conservé, MTTR faux | code |
| M5 | Recherche de problèmes sensible à la casse | code |
| M6 | Action à échéance du jour signalée en retard dès le matin (fuseau) | code |

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
| S4 | **Migrations EF** à la place d'`EnsureCreated` ([détail](reference/base-de-donnees.md#gestion-du-schéma)) | code |
| S5 | Stockage du jeton côté navigateur (`sessionStorage` ou cookie `HttpOnly`) | décision |

## 6. Qualité

| # | Chantier | Qui |
|---|---|---|
| Q1 | Socle de **tests** : commencer par les scénarios de la revue fonctionnelle (B1–B3, R1–R4) | code |
| Q2 | **CI** : build backend et frontend, `python3 scripts/check-docs.py` | code |
| Q3 | Retirer `puppeteer-core` des dépendances du frontend (inutilisé, alourdit l'image) | décision |
| Q4 | Créer les issues GitHub des sections 1 à 6 et les lier ici | action |

## 7. Feuille de route fonctionnelle

Processus « Bientôt » ([cartographie](reference/produit.md#cartographie-des-processus-itil-et-état)).
Ordre à arbitrer, **après** les sections 1 à 3.

| # | Processus | Remarque |
|---|---|---|
| F1 | Incidents | Lien naturel vers les problèmes (incident → problème) |
| F2 | Changements | Lien naturel depuis les actions correctives |
| F3 | Demandes, Configuration, Niveaux de service, Connaissances, Amélioration (CSI) | — |
