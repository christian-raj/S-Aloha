# ADR-0019 — Administration : utilisateurs depuis l'annuaire, fusion des doublons, catégories

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-19
- **Statut** : accepté le 2026-10-19
- **Décideur** : Christian Rajaonary

## Contexte

Le chantier A1 (« console d'administration inexistante ») a été décidé dans son principe le
2026-10-07 : une section **Administration** (SOC-26) et une section **Paramétrage** (SOC-27).
Deux entrées restaient trop floues pour être construites : « utilisateurs constatés » (la
plateforme ne stocke aucun compte, l'identité vient de l'AD) et « nettoyage des doublons ».
Spécification : [administration](../reference/administration.md).

## Décision

1. **Utilisateurs** : la liste est celle des membres des groupes AD de rôle, lue en direct
   dans l'annuaire, avec le rôle effectif et les affectations de périmètre. L'API retient la
   seule **date de dernière connexion** de chaque compte, effacée après 13 mois sans
   connexion.
2. **Doublons** : détection des doublons probables (même pratique, même périmètre, titre
   normalisé identique, créés à 7 jours au plus) et **fusion** par l'Admin (liens et
   commentaires reportés sur l'enregistrement conservé, doublon supprimé, opération tracée),
   ou « pas un doublon ».
3. **Catégories** (SOC-27) : réalisées avec A1, dans Paramétrage, tenues par les
   gestionnaires ; les catégories existantes sont reprises.

## Conséquences

- Une table de dernière connexion (compte, date) et une purge quotidienne ; à mentionner au
  registre des traitements de l'organisation.
- Une table des paires écartées (« pas un doublon ») et une table des catégories.
- La fusion n'emporte pas les éléments propres à une pratique (analyses, actions, décisions du
  CAB) : un tel doublon se traite à la main.

## Alternatives envisagées

- **Annuaire seul**, sans rien stocker — écarté : sans dernière connexion, les comptes
  inactifs ne se repèrent pas.
- **Comptes constatés à la connexion seulement** — écarté : un membre qui ne s'est jamais
  connecté n'apparaîtrait pas.
- **Suppression simple des doublons** — écartée : liens et commentaires du doublon seraient
  perdus.
