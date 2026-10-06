# Plan d'action

> **Étape 2 de la méthode : ce qu'il reste à faire.** Alimenté par les constats
> ([`archives/`](archives/)), vidé au fur et à mesure que les chantiers ferment ou
> deviennent des décisions ([`decisions/`](decisions/)).
>
> Le statut ouvert/fermé de chaque bug vit dans
> [GitHub Issues](https://github.com/christian-raj/S-Aloha/issues), source de vérité
> depuis [ADR-0001](decisions/adr-0001-github-issues-source-de-verite.md). Ce document
> porte les **chantiers** — ce qui demande une intention, pas seulement un correctif.
>
> *Établi le 2026-10-06 à partir de [`archives/etat-initial-2026-10-06.md`](archives/etat-initial-2026-10-06.md).*

## Bloquants avant une mise en production

| # | Chantier | Qui | Détail |
|---|---|---|---|
| 1 | **Secrets hors du dépôt** : `Jwt:Key`, mots de passe PostgreSQL et LDAP injectés (secrets Docker / variables d'environnement), valeurs d'exemple neutralisées | code + action | [`securite.md`](reference/securite.md#points-de-durcissement-avant-production) |
| 2 | **LDAPS** et **HTTPS** | action | idem |
| 3 | **CORS** restreint, API et Swagger non publiés hors de nginx | code | idem |
| 4 | **Migrations EF** à la place d'`EnsureCreated` — prérequis à toute évolution du modèle sur une base en service | code | [`base-de-donnees.md`](reference/base-de-donnees.md#gestion-du-schéma) |

## Qualité

| # | Chantier | Qui |
|---|---|---|
| 5 | Socle de **tests** : règles métier (priorité, RACI, cycle de vie) côté API en premier | code |
| 6 | **CI** : build backend + frontend, contrôle de la documentation (`python3 scripts/check-docs.py`) | code |
| 7 | Stockage du jeton côté navigateur (`sessionStorage` vs cookie `HttpOnly`) | décision |

## Feuille de route fonctionnelle

Processus ITIL déclarés « Bientôt » dans le registre
([`produit.md`](reference/produit.md#cartographie-des-processus-itil-et-état)). Ordre à
arbitrer ; chacun ouvrira un ADR s'il impose un choix structurant (liens entre processus,
CMDB).

| # | Processus | Remarque |
|---|---|---|
| 8 | Incidents | Lien naturel vers les problèmes (incident → problème) |
| 9 | Changements | Lien naturel depuis les actions correctives |
| 10 | Demandes, Configuration, Niveaux de service, Connaissances, Amélioration (CSI) | — |

## Documentation

| # | Chantier | Qui |
|---|---|---|
| 11 | Créer sur GitHub les issues correspondant aux points 1 à 7 (labels `security`, `enhancement`) et les lier ici | action |
