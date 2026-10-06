# 🧭 Produit

> Pourquoi S-Aloha existe, pour qui, et quels processus ITIL il couvre.
>
> *L'excellence du service IT au cœur de notre performance.* — « Ny fahaiza-manao ho amin'ny tolotra tsara kokoa. »

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## Pourquoi S-Aloha existe

S-Aloha est une plateforme d'**intégration des processus ITIL** pour une DSI d'entreprise :
un seul outil, adossé à l'**Active Directory** de l'organisation, pour gérer les processus
du cycle de vie du service IT — incidents, problèmes, changements, configuration, niveaux
de service, connaissances, amélioration continue.

Le nom joue sur deux lectures : **S-A-L-O-H-A**, acronyme des six piliers ci-dessous, et
*aloha*, « d'abord, avant tout » en malgache — le service passe avant tout
(*Service alohan'ny zavatra rehetra*). Voir le [glossaire](glossaire.md#vocabulaire-malgache).

## Pour qui

| Profil | Rôle applicatif | Ce qu'il fait dans S-Aloha |
|---|---|---|
| Collaborateur IT (support, exploitation, études) | **User** | Déclare, analyse, réalise les actions qui lui sont affectées |
| Gestionnaire de processus (*process manager*) | **Manager** | Qualifie, pilote les statuts, relance, valide les causes racines |
| Administrateur de la plateforme | **Admin** | Tous les droits, y compris la suppression |

Les rôles sont dérivés des groupes AD ; le détail des droits est dans
[`regles-metier.md`](regles-metier.md#1-rôles-et-droits).

## Les six piliers

Chaque processus ITIL est rattaché à un pilier ; les libellés sont ceux de la charte
S-Aloha (« Alignment » compris). Source dans le code :
`PILLARS` dans `frontend/src/modules/registry.js`.

| Lettre | Pilier | Ce qu'il recouvre |
|---|---|---|
| **S** | Service | Gestion des services et des incidents |
| **A** | Alignment | Processus IT alignés sur les besoins métier |
| **L** | Leadership | Accompagnement du changement et formation |
| **O** | Optimisation | Amélioration continue (CSI) |
| **H** | Harmonie | Cohésion entre équipes IT et utilisateurs |
| **A** | Agilité | Changements et mises en production fluides |

## Cartographie des processus ITIL et état

Ordre du cycle de vie du service. Source unique : `MODULES` dans
`frontend/src/modules/registry.js` — un processus passe de **Bientôt** à **Actif** le jour
où son interface existe ([ADR-0003](../decisions/adr-0003-plateforme-modulaire-par-processus-itil.md)).

| Processus | Objet | Pilier | État | Module |
|---|---|---|---|---|
| Incidents | Rétablir le service au plus vite | Service | Bientôt | — |
| Demandes | Demandes de service utilisateurs | Service | Bientôt | — |
| **Problèmes** | Causes racines et erreurs connues | Service | **Actif** | `Modules/ProblemManagement`, `modules/problem-management` |
| Changements | Changements et mises en production | Agilité | Bientôt | — |
| Configuration | Actifs et dépendances (CMDB) | Service | Bientôt | — |
| Niveaux de service | Catalogue, SLA et engagements | Alignment | Bientôt | — |
| Connaissances | Capitalisation et formation | Leadership | Bientôt | — |
| Amélioration (CSI) | Amélioration continue des services | Optimisation | Bientôt | — |

> Le pilier **Harmonie** n'a pas encore de processus rattaché.

## Socle transverse (Pilotage)

Indépendants des processus, accessibles à tous les rôles :

- **Ma console** — page d'accueil orientée action, composée côté serveur selon le rôle
  (règles : [`regles-metier.md`](regles-metier.md#2-consoles-par-rôle)) ;
- **Reporting** — indicateurs et volumétrie.

## Processus actif : Gestion des problèmes (ITIL v3)

1. **Détection et enregistrement** : déclaration avec impact × urgence → priorité P1–P4
   calculée, référence `PRB-AAAA-NNNN`.
2. **Investigation et diagnostic** : analyses de cause racine — **5 Pourquoi**,
   **Ishikawa (6M)**, **arbre des défaillances (FTA)** avec portes ET/OU.
3. **Erreur connue** : statut dédié et contournement documenté.
4. **Résolution** : actions correctives avec échéance et matrice **RACI**, chaque rôle
   affecté à un utilisateur ou groupe AD.
5. **Suivi et clôture** : suivi transverse des actions, retards signalés, MTTR.

Règles détaillées : [`regles-metier.md`](regles-metier.md).

## Ce que S-Aloha refuse de devenir

- **Un outil SaaS multi-locataire** : S-Aloha se déploie on-prem, chez une organisation,
  contre son Active Directory.
- **Un gestionnaire de comptes** : aucun mot de passe ni utilisateur n'est stocké ;
  l'identité et les rôles viennent de l'AD.
- **Une collection d'applications** : les processus partagent un socle (identité,
  annuaire, console, reporting) et une navigation uniques.
