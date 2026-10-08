# 🧭 Produit

> Pourquoi S-Aloha existe, pour qui, et quels processus ITIL il couvre.
>
> « Ny fahaiza-manao ho amin'ny tolotra tsara kokoa. »

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## Pourquoi S-Aloha existe

S-Aloha est une plateforme d'**intégration des processus ITIL** pour une DSI d'entreprise :
un seul outil, adossé à l'**Active Directory** de l'organisation, pour gérer les processus
du cycle de vie du service IT — incidents, problèmes, changements, configuration, niveaux
de service, connaissances, amélioration continue.

Le nom vient du malgache *aloha*, « d'abord, avant tout » : le service passe avant tout
(*Service alohan'ny zavatra rehetra*). Voir le [glossaire](glossaire.md#vocabulaire-malgache).

## Pour qui

| Profil | Rôle applicatif | Ce qu'il fait dans S-Aloha |
|---|---|---|
| Collaborateur IT (support, exploitation, études) | **User** | Déclare, analyse, réalise les actions qui lui sont affectées |
| Gestionnaire de processus (*process manager*) | **Manager** | Qualifie, pilote les statuts, relance, valide les causes racines |
| Administrateur de la plateforme | **Admin** | Tous les droits, y compris la suppression |

Les rôles sont dérivés des groupes AD ; le détail des droits est dans
[`regles-metier.md`](regles-metier.md#1-rôles-et-droits).

## Cartographie des processus ITIL et état

Ordre du cycle de vie du service. Source unique : `MODULES` dans
`frontend/src/modules/registry.js` — un processus passe de **Bientôt** à **Actif** le jour
où son interface existe ([ADR-0003](../decisions/adr-0003-plateforme-modulaire-par-processus-itil.md)).

| Processus | Objet | État | Module (API, interface) |
|---|---|---|---|
| **Incidents** | Rétablir le service au plus vite | Actif (MVP) | `Modules/IncidentManagement`, `modules/incident-management` |
| **Demandes** | Demandes de service utilisateurs | Actif (MVP) | `Modules/ServiceRequestManagement`, `modules/service-request-management` |
| **Problèmes** | Causes racines et erreurs connues | Actif | `Modules/ProblemManagement`, `modules/problem-management` |
| **Changements** | Changements et mises en production | Actif (MVP) | `Modules/ChangeEnablement`, `modules/change-enablement` |
| **Configuration** | Actifs et dépendances (CMDB) | Actif (MVP) | `Modules/ServiceConfigurationManagement`, `modules/service-configuration-management` |
| **Niveaux de service** | Catalogue, SLA et engagements | Actif (MVP) | `Modules/ServiceLevelManagement`, `modules/service-level-management` |
| **Connaissances** | Capitalisation et formation | Actif (MVP) | `Modules/KnowledgeManagement`, `modules/knowledge-management` |
| **Amélioration (CSI)** | Amélioration continue des services | Actif (MVP) | `Modules/ContinualImprovement`, `modules/continual-improvement` |
| **Conformité NIS 2** | Évaluer la conformité au Référentiel Cyber France (ANSSI) | Actif (MVP) | `Modules/ComplianceAssessment`, `modules/compliance-assessment` |

**MVP** : enregistrer, suivre un cycle de vie simple, porter les décisions des
gestionnaires et relier les processus entre eux. Référentiel : les **pratiques ITIL 4**
publiées par PeopleCert/Axelos ([ADR-0007](../decisions/adr-0007-pratiques-itil4-et-socle-commun-des-processus.md)) ;
l'enrichissement de chaque pratique viendra ensuite ([plan d'action](../plan-action.md)).

## Socle transverse (Pilotage)

Indépendants des processus, accessibles à tous les rôles :

- **Ma console** — page d'accueil orientée action, composée côté serveur selon le rôle
  (règles : [`regles-metier.md`](regles-metier.md#2-consoles-par-rôle)) ;
- **Reporting** — indicateurs (problèmes, MTTR incidents, taux de changements réussis)
  et volumétrie par processus.

## Les processus

Chaque processus reprend l'objectif de la pratique ITIL 4 correspondante. Règles
détaillées, pratique par pratique : [`processus/`](processus/readme.md).

| Processus | Objectif ITIL 4 | Ce que fait le MVP |
|---|---|---|
| Incidents | Minimiser l'impact négatif des incidents en rétablissant le service normal au plus vite | Déclaration, priorité P1–P4, assignation AD, incident majeur, résolution ; ouverture d'un problème lié |
| Demandes | Délivrer la qualité de service convenue en traitant les demandes prédéfinies des utilisateurs, de façon efficace et conviviale | Objet demandé, bénéficiaire, échéance ; approbation par un gestionnaire avant traitement |
| Changements | Maximiser le nombre de changements réussis : évaluer les risques, autoriser, gérer le calendrier | Types Standard (pré-autorisé), Normal, Urgent ; risque ; autorisation ; plans de mise en œuvre et de retour arrière ; résultat ; calendrier |
| Configuration | Une information exacte et fiable sur les services et les CI, quand et où elle est nécessaire | CI typés par environnement, propriétaire ; relations entre CI |
| Niveaux de service | Des cibles claires, orientées métier, et le suivi de la prestation au regard de ces cibles | Catalogue des services ; SLA (disponibilité, délais de résolution P1–P4, revue) |
| Connaissances | Un usage efficace, efficient et pratique de l'information et des connaissances | Articles (solution, procédure, erreur connue, FAQ), publication validée, revue |
| Amélioration continue | Aligner les services sur l'évolution des besoins par l'amélioration continue | Registre d'amélioration, mesures de départ et cible, modèle ITIL 4 en 7 étapes, validation |

Tous les enregistrements se **relient** entre eux par leur référence (incident → problème,
problème → changement, changement → CI, problème → article d'erreur connue…).

### Gestion des problèmes

1. **Détection et enregistrement** : déclaration avec impact × urgence → priorité P1–P4
   calculée, référence `PRB-AAAA-NNNN`.
2. **Investigation et diagnostic** : analyses de cause racine — **5 Pourquoi**,
   **Ishikawa (6M)**, **arbre des défaillances (FTA)** avec portes ET/OU.
3. **Erreur connue** : statut dédié et contournement documenté.
4. **Résolution** : actions correctives avec échéance et matrice **RACI**, chaque rôle
   affecté à un utilisateur ou groupe AD.
5. **Suivi et clôture** : suivi transverse des actions, retards signalés, MTTR.

Règles détaillées : [gestion des problèmes](processus/gestion-des-problemes.md).

## Ce que S-Aloha refuse de devenir

- **Un outil SaaS multi-locataire** : S-Aloha se déploie on-prem, chez une organisation,
  contre son Active Directory.
- **Un gestionnaire de comptes** : aucun mot de passe ni utilisateur n'est stocké ;
  l'identité et les rôles viennent de l'AD.
- **Une collection d'applications** : les processus partagent un socle (identité,
  annuaire, console, reporting) et une navigation uniques.
