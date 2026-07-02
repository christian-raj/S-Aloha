# Règles de gestion — Gestion des problèmes (ITIL v3)

Ce document décrit les règles métier implémentées dans l'application. Toute évolution du code doit rester cohérente avec ces règles, ou celles-ci doivent être mises à jour.

## 1. Rôles et droits

Les rôles applicatifs sont dérivés de l'appartenance aux **groupes Active Directory** définis dans `backend/appsettings.json` (section `Ldap:Groups`). Un utilisateur hors de ces groupes ne peut pas se connecter.

| Capacité | User | Manager | Admin |
|---|---|---|---|
| Se connecter, consulter problèmes / analyses / actions / reporting | ✔ | ✔ | ✔ |
| Déclarer un problème | ✔ | ✔ | ✔ |
| Créer / modifier une analyse RCA | ✔ | ✔ | ✔ |
| Créer / mettre à jour une action corrective et sa matrice RACI | ✔ | ✔ | ✔ |
| Modifier un problème (titre, statut, cause racine, contournement) | ✘ | ✔ | ✔ |
| Supprimer une analyse ou une action | ✘ | ✔ | ✔ |
| Supprimer un problème | ✘ | ✘ | ✔ |

Si un utilisateur appartient à plusieurs groupes, le rôle le plus élevé est retenu (ordre d'évaluation : Admin > Manager > User).

## 2. Consoles par rôle

Chaque rôle dispose de sa propre console (page d'accueil « Ma console ») :

- **User** : ses actions à traiter (avec ses rôles RACI et les retards), ses problèmes déclarés encore ouverts.
- **Manager** : le bloc User **+** le pilotage du processus — problèmes à qualifier (statut Nouveau), analyses en cours sans cause racine, erreurs connues sans action corrective, actions en retard toutes équipes.
- **Admin** : les blocs User et Manager **+** l'administration — volumétrie globale (problèmes, actions, analyses, déclarants) et dernière activité.

La composition des blocs est décidée **côté serveur** (`GET /api/console`) à partir du rôle porté par le jeton : un client ne peut pas obtenir un bloc qui ne correspond pas à son rôle.

## 3. Cycle de vie d'un problème

`Nouveau → En analyse → Erreur connue → Résolu → Clos`

- Un problème est créé au statut **Nouveau** avec une référence `PRB-AAAA-NNNN` (séquence annuelle).
- La création d'une première analyse RCA fait passer automatiquement un problème **Nouveau** à **En analyse**.
- **Erreur connue** : le champ *Contournement* doit être documenté (règle de bonne pratique, non bloquante).
- Le passage à **Clos** horodate `ClosedAt`, utilisé pour le calcul du MTTR. Seuls Manager et Admin changent les statuts.

## 4. Priorité

La priorité est **calculée automatiquement** (matrice impact × urgence), non modifiable directement :

| | Urgence Faible | Urgence Moyenne | Urgence Élevée |
|---|---|---|---|
| **Impact Faible** | P4 | P4 | P3 |
| **Impact Moyen** | P4 | P3 | P2 |
| **Impact Élevé** | P3 | P2 | P1 |

## 5. Analyses de cause racine

- Méthodologies disponibles : **5 Pourquoi**, **Ishikawa (6M)**, **Arbre des défaillances (FTA)** avec portes ET/OU.
- Plusieurs analyses (y compris de méthodes différentes) peuvent coexister sur un même problème.
- La « Cause racine identifiée » d'une analyse alimente son champ *Conclusion* ; la cause racine **validée** du problème est renseignée par un Manager dans la fiche du problème.

## 6. Actions correctives et matrice RACI

- Chaque action porte : titre, description, échéance, statut (`À faire, En cours, Terminée, Annulée`).
- Règles RACI **bloquantes à la création** (validées côté API) :
  - au moins **un R** (Responsable — réalise l'action) ;
  - exactement **un A** (Approbateur — rend compte du résultat) ;
  - C (Consulté) et I (Informé) libres.
- Chaque rôle RACI est affecté à un **utilisateur ou groupe AD** (recherche en direct dans l'annuaire).
- Une action est **en retard** si son échéance est dépassée et son statut ni Terminée ni Annulée.
- Le passage à **Terminée** horodate `CompletedAt`.

## 7. Reporting

- **MTTR** : moyenne en jours de (`ClosedAt − CreatedAt`) sur les problèmes clos.
- Répartitions par statut, priorité et catégorie ; liste des actions en retard avec leurs responsables (R).
