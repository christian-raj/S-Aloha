# ADR-0014 — Périmètres : droits scopés, affectations d'utilisateurs et de groupes AD

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-10
- **Statut** : accepté le 2026-10-10 — décisions D1 à D4 selon les recommandations
- **Décideur** : Christian Rajaonary

## Contexte

Le rôle d'un utilisateur (Admin, Manager, User) vient de ses groupes Active Directory et vaut
pour toute la plateforme ([règles métier § 1](../reference/regles-metier.md#1-rôles-et-droits)).
Une DSI qui sert plusieurs directions, sites ou entités a besoin de limiter les décisions de
gestionnaire — et souvent la visibilité — au périmètre de chacun. La spécification est dans
[périmètres](../reference/perimetres.md) (règles PER-01 à PER-12).

## Décision

1. **Admin global** ; **Manager** et **User** scopés par **affectations** (principal, rôle,
   périmètre), plusieurs par compte. Le groupe AD de rôle reste la **porte d'entrée** (accès,
   statut d'Admin) ; la portée vient des affectations.
2. Chaque enregistrement de pratique porte un **périmètre** obligatoire ; les droits de
   gestionnaire s'exercent sur le périmètre de l'enregistrement.
3. Gestion par l'**Admin** dans **Administration › Périmètres** : c'est un réglage de la
   plateforme (accès et rôles), pas du fonctionnement d'une pratique.

## Décisions D1 à D4

| # | Question | Options | Décision (recommandation acceptée) |
|---|---|---|---|
| D1 | Qui peut-on affecter ? | Utilisateurs AD seulement / groupes AD seulement / les deux | **Les deux** : le groupe pour l'échelle (une équipe entière), l'utilisateur pour l'exception. Suppose les groupes dans le jeton (étape 5, SOC-10) |
| D2 | Visibilité hors périmètre | Rien / lecture seule de tout / lecture des seuls référentiels partagés | **Référentiels partagés** : catalogue des services et articles publiés visibles de tous ; le reste limité aux périmètres |
| D3 | Installations existantes | Périmètre par défaut et droits inchangés tant qu'aucune affectation / affectations obligatoires dès la mise à niveau | **Périmètre « Organisation » par défaut**, droits inchangés tant que l'Admin n'a rien affecté : la mise à niveau ne casse rien |
| D4 | Rôle AD et rôle par périmètre | Le rôle AD plafonne / le rôle par périmètre prime | **Le rôle par périmètre prime** (un User AD peut être Manager d'un périmètre) ; le groupe AD ne décide que de l'accès et du statut d'Admin |

## Conséquences

- Migration : table des périmètres et des affectations, colonne `PerimeterId` sur chaque
  enregistrement (reprise sur « Organisation »).
- Le contrôle des droits passe d'un rôle global (policies) à un **rôle effectif par
  enregistrement**, calculé dans le socle (`RecordController`, module Problèmes) ; les listes,
  la console, le reporting et la recherche filtrent par périmètre.
- Les tests d'API doivent couvrir chaque décision de gestionnaire hors périmètre (403).

## Alternatives envisagées

- **Rester sur le rôle global** — écarté dès qu'une DSI sert plusieurs entités : un gestionnaire
  déciderait hors de son ressort.
- **Une instance par entité** — écarté : les processus sont transverses (un incident d'un site
  relève d'un changement du siège), et l'annuaire est commun.
- **Périmètres dans l'annuaire seulement** (un groupe AD par périmètre et par rôle) — écarté
  comme seule voie : rigide et dépendant de l'équipe AD ; il reste possible au travers des
  affectations de groupes (D1).
