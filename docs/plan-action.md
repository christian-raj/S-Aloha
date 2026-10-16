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
([journal](archives/journal-2026-10-06.md)) ; M7 soldé le 2026-10-11 (PRB-24), M3 le
2026-10-12 (groupes AD dans le jeton, SOC-10, PRB-26). M8 attend une décision (section 4).

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
| Q4 | Créer les issues GitHub des sections 1 à 7 et les lier ici | action |

## 7. Feuille de route fonctionnelle

Les dix pratiques tiennent leur cycle de vie complet ; toutes ont quitté le stade MVP le
2026-10-16 ([§ Sortie du MVP](#sortie-du-mvp), [cartographie](reference/produit.md#cartographie-des-processus-itil-et-état),
[ADR-0007](decisions/adr-0007-pratiques-itil4-et-socle-commun-des-processus.md)). Les évolutions
restantes sont spécifiées règle par règle dans [`reference/processus/`](reference/processus/readme.md)
(règles 🔜 des lots 2 et 3).

| # | Processus | Enrichissement | Règles |
|---|---|---|---|
| F2 | Incidents, Problèmes | Service et catégorie tirés du catalogue et d'un référentiel | [SOC-27](reference/regles-metier.md#9-traçabilité-commentaires-administration), [SLM-10](reference/processus/gestion-des-niveaux-de-service.md) |
| F3 | Demandes | Tâches d'exécution créées depuis le modèle (catalogue et modèles faits le 2026-10-14) | [REQ-24, REQ-25](reference/processus/gestion-des-demandes.md) |
| F5 | Configuration | Import en masse, attributs par type de CI, service signalé « dégradé » (rattachement CI ↔ service fait le 2026-10-13) | [CFG-16, CFG-18, CFG-30](reference/processus/gestion-de-la-configuration.md) |
| F7 | Tous | Pièces jointes, notifications par courriel (notifications dans l'application et commentaires faits le 2026-10-13) | [SOC-22, SOC-25](reference/regles-metier.md#8-notifications) |
| F8 | Connaissances, Incidents | Suggestion d'articles et cas similaires dès la saisie, relier en un clic (rendu Markdown fait le 2026-10-12) | [INC-19](reference/processus/gestion-des-incidents.md), [RAG-09, RAG-11](reference/recherche.md) |
| F11 | Incidents, sécurité | **Déclarations réglementaires** des incidents cyber (NIS 2 : alerte précoce 24 h, notification 72 h, rapport final ; RGPD : 72 h) associées à l'incident, échéances et alertes | [INC-40 à INC-44](reference/processus/gestion-des-incidents.md), [DRG-01 à DRG-13](reference/processus/declarations-reglementaires.md) |
| F12 | Conformité | 🔜 **Évaluations sur référentiels définis par l'Admin** (ex. CR-CMM, ISO 27002) : l'Admin importe un référentiel en CSV depuis sa console en choisissant les colonnes (domaine ou pratique, niveau, identifiant, question, preuve attendue) et règle l'échelle (ex. Implémenté = 1, Partiel = 0,5, Non = 0 ; un niveau n'est atteint que si tous ses points de contrôle et ceux des niveaux inférieurs sont implémentés). Le contenu est importé par l'organisation et **jamais versionné** dans le dépôt, même logique que l'[ADR-0012](decisions/adr-0012-referentiel-nis2-structure-versionnee-texte-importe.md) : CR-CMM est sous licence CC BY-NC 4.0, usage interne libre, œuvre dérivée ou revente soumises à l'accord écrit de ses auteurs (vérifié le 2026-10-12). Place : section Conformité & réglementation | À spécifier |

Ces chantiers sont décidés : leurs règles sont écrites et classées en lots
([plan d'implémentation du lot 1](reference/processus/readme.md#plan-dimplémentation-conseillé--lot-1)).

**Lot 1** : terminé le 2026-10-13 (étapes 1 à 6, périmètres compris, NIS-18), sauf RAG-10
(calibrage de la recherche), **en attente** d'un jeu de requêtes réelles (décision du
2026-10-13). **Itération en cours** : règles 🔜 du lot 2, et
[déclarations réglementaires](reference/processus/declarations-reglementaires.md) (F11).

### Sortie du MVP

**Toutes les pratiques sont sorties du MVP le 2026-10-16.** Une pratique perdait la mention
**(MVP)** quand les prérequis communs étaient en place — lot 1, commentaires (SOC-24) et
notifications (SOC-21), tous faits le 2026-10-13 — et que toutes ses règles de sortie
(cœur de métier du lot 2) étaient à ✅ :

| Date | Pratiques | Réserves acceptées |
|---|---|---|
| — | Problèmes (pratique de référence, jamais en MVP ; dernières règles PRB-10, PRB-25, PRB-27 le 2026-10-13) | |
| 2026-10-13 | Connaissances, Configuration, Conformité NIS 2 | RAG-10 en attente ; pièces jointes de NIS-08 avec SOC-25 |
| 2026-10-14 | Niveaux de service, Amélioration continue, Incidents, Demandes | SLM-21 et tâches (REQ-24, REQ-25) au lot 3 ; modèle de demande facultatif ; cible de conformité d'un SLA facultative |
| 2026-10-16 | Changements, Projets | CI concernés d'un modèle standard à venir |

Le détail règle par règle est dans les journaux du
[2026-10-13](archives/journal-2026-10-13.md), du [2026-10-14](archives/journal-2026-10-14.md)
et du [2026-10-16](archives/journal-2026-10-16.md).

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
