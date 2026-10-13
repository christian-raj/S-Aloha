# Journal des versions

Les changements notables de chaque version, pour qui installe et met à jour S-Aloha.
Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) ; versions selon
le [versionnage sémantique](https://semver.org/lang/fr/). Tant que le projet est en `0.x`,
une version mineure peut modifier la configuration ou le schéma : lire la section
**Mise à jour** avant d'installer.

Le détail au jour le jour (sujets traités, raisons, commits) est dans les
[journaux](docs/archives/readme.md) ; les notes complètes de chaque version dans les
[releases](https://github.com/christian-raj/S-Aloha/releases).

## [Non publié]

### Ajouté

- **Périmètres — vues transverses** (lot 1, étape 6) : un lien vers un enregistrement d'un
  autre périmètre reste visible mais masqué ; la console, le reporting, la recherche et les
  cas similaires se limitent aux périmètres de l'utilisateur, le pilotage de la console aux
  périmètres qu'il gère ; filtre « Périmètre » dans la console et le reporting (PER-08,
  PER-09).

- **Périmètres — droits et visibilité** (lot 1, étape 6, deuxième partie) : chacun voit les
  enregistrements de ses périmètres (plus le catalogue des services et les articles publiés)
  et crée dans l'un d'eux (choix proposé dès qu'il en a plusieurs) ; les décisions de
  gestionnaire exigent d'être Manager du périmètre de l'enregistrement, et le rôle par
  périmètre prime sur le rôle AD ; déplacer un enregistrement exige d'être gestionnaire des
  deux périmètres, et se trace au journal. Un compte sans affectation garde son rôle AD sur
  « Organisation » (PER-03 à PER-07, PER-10 amendé).

- **Connaissances au-delà du MVP** : contenu rendu en **Markdown** sans HTML brut (onglet
  Lecture, KB-10) ; date de revue fixée à un an à la publication (KB-11) ; article
  d'erreur connue créé depuis le problème, puis proposé à l'archivage quand le problème est
  corrigé (PRB-30, KB-12) ; utilisation de l'article (incidents et demandes reliés, KB-14) ;
  revue confirmée par le responsable ou archivage demandé (KB-17) ; archiver et réactiver
  réservés aux gestionnaires (KB-18) ; brouillon soumis à publication, qui remonte dans les
  décisions des gestionnaires (KB-21).

- **Périmètres — modèle et administration** (lot 1, étape 6, première partie) : périmètres
  (direction, site, entité) avec suppression douce, restauration et suppression définitive
  contrôlée ; affectations d'utilisateurs ou de groupes AD avec le rôle Manager ou User ;
  compteurs par pratique ; tout tracé au journal d'audit. À la mise à niveau, chaque
  enregistrement est rattaché au périmètre « Organisation » : les droits sont inchangés
  (PER-01, PER-02, PER-10 à PER-12).

- **Reporting** : tableau de bord à onglets — synthèse globale avec score de santé expliqué,
  incidents, problèmes, changements et projets, conformité et métriques ; filtres période
  (7, 30, 90 jours, 12 mois, personnalisée) et service ; badges d'alerte sur les onglets ;
  export CSV de l'onglet actif (Excel) et PDF de tous les onglets (règles REP).
- **Incidents** : échéance de résolution tirée du SLA en vigueur du service (INC-21,
  partiel), résolution au premier contact (INC-23), début constaté et MTTD (INC-27).

- **Projets** (gestion de projet, ADR-0015) : la méthode — Scrum, Kanban, Cycle en V,
  PRINCE2 ou hybride — active les composants du projet (backlog, sprints et burndown,
  tableau, phases ou étapes, jalons, livrables, risques, incidents projet, business case,
  tolérances). Le chef de projet crée et conduit, un gestionnaire valide le lancement ;
  synthèse d'avancement et alertes de tolérance. Nouvelle section de menu **Projets**
  (repère vert) entre Pratiques et Conformité & réglementation.

- **Groupes AD dans le jeton** (lot 1, étape 5) : un incident, une demande, un changement,
  une amélioration ou une action confiés à un groupe AD apparaissent dans « Mon travail »,
  « Assignés à moi » et « Mes actions » de chacun de ses membres ; un membre du groupe
  désigné R ou A d'une action en a les droits (SOC-10, PRB-26 — résout M3).

- **Navigation** : section **Conformité & réglementation** (repère violet, tous les rôles)
  entre Pratiques et Paramétrage ; « Conformité NIS 2 » y passe sous le libellé « NIS 2 »,
  la section Pratiques ne garde que les 8 pratiques ITIL. Sections repliables (sauf
  Pilotage), choix mémorisé dans le navigateur ; la section de la page ouverte reste dépliée.

- **Conformité NIS 2** : évaluation au Référentiel Cyber France de l'ANSSI (catégorie
  d'entité, questionnaire noté 0 à 3, scores par thématique, objectif et pilier, maturité,
  écarts, actions d'amélioration) ; structure du référentiel embarquée, texte des exigences
  importé par l'administrateur (ADR-0012). Première entrée de la section Paramétrage.
- **Recherche hybride** (mots et sens) : bloc « Chercher d'abord » dans les connaissances,
  « Cas similaires » sur les fiches incident et problème, Administration › Index de
  recherche ; modèle `bge-m3` dans le nouveau service `embeddings` (facultatif, environ
  1,2 Go téléchargés au premier démarrage) ; sans lui, recherche plein texte (ADR-0013).
- Configuration : vue d'impact d'un CI, amont et aval, transitive (#30).
- Changements : détection des conflits au calendrier, deux changements non rejetés sur un
  même CI et des créneaux qui se chevauchent (#29).
- **Historique** sur chaque fiche : journal d'audit des créations, champs modifiés
  (ancienne et nouvelle valeur), transitions, suppressions et liens, avec auteur et date ;
  conservé 3 ans (SOC-20).
- **Transition forcée** par un administrateur, hors du cycle de vie, avec un motif
  obligatoire tracé à l'historique (SOC-05).

- **Droits et statuts — étape 4 du lot 1** :
  - Incidents : réouverture encadrée — fenêtre de 10 jours ouvrés, motif obligatoire (à
    renouveler à chaque réouverture), compteur `ReopenCount` (INC-11) ; caractère majeur
    déclaré ou retiré par un Manager seulement, priorité forcée P1, responsable nominatif
    (pas un groupe) désigné par un Manager (INC-13).
  - Demandes de service : statut **Annulée** (final), annulation réservée au demandeur ou
    à un Manager, date `CancelledAt` tracée (REQ-09).
  - Configuration : retrait d'un CI réservé au Manager (CFG-12), bloqué si un incident ou
    changement ouvert est lié au CI (CFG-13), liaison interdite vers ou depuis un CI Retiré
    (CFG-14).
  - Problèmes : modification d'une action (titre, description, échéance, RACI) réservée
    au R, à l'A ou à un Manager ; la mise à jour du seul statut reste ouverte à tous
    (PRB-24 — résout M7).

### Modifié

- **Problèmes sur le socle commun** : le module Problèmes repose désormais, comme les autres
  pratiques, sur `Record` et `RecordController` (registre, transitions, transition forcée,
  périmètres, journal) ; il gagne un **gestionnaire du problème** (responsable AD, par défaut
  le gestionnaire qui qualifie, dans son « Mon travail », PRB-21). API inchangée pour les
  clients existants.
- **NIS-15** : créer une action d'amélioration depuis un écart est désormais réservé aux
  gestionnaires (du périmètre de l'évaluation), comme l'indique le tableau des droits ; l'API
  l'acceptait de tout utilisateur.
- **Validations du cycle de vie** (lot 1, étape 3) : prise en charge d'un incident avec un
  responsable (date de première prise en charge) ; code de résolution, un doublon relié à
  l'incident conservé ; motif de rejet des demandes et des changements ; plans exigés à
  l'évaluation d'un changement, fin planifiée ; propriétaire d'un CI ou d'un service en
  service ; nom de CI unique par environnement ; SLA en vigueur cohérent (cibles complètes et
  croissantes, un seul par service et client) ; amélioration mesurable à la validation,
  réalisée à l'étape 6, abandon motivé ; problèmes : valeurs fermées, contournement d'une
  erreur connue, cause racine et actions terminées pour résoudre, code de clôture, règles
  RACI à la modification d'une action, date d'achèvement conservée (#23, #24, #28).
- **Transitions de statut contraintes** (SOC-05) : chaque pratique n'accepte que les
  transitions de son cycle de vie documenté ; une autre est refusée avec les statuts
  accessibles, et la fiche ne propose plus que ceux-là. Statuts finaux : incident clos,
  demande rejetée ou close, changement rejeté ou clos, CI retiré, SLA expiré, amélioration
  réalisée.
- Page de connexion : titre « Piloter le service IT, de l'incident à l'amélioration. » ;
  sous-titre de marque « Plateforme ITIL ».

### Mise à jour

- `docker compose up` démarre désormais le service `embeddings` et télécharge le modèle
  `bge-m3` (environ 1,2 Go) au premier démarrage ; pour s'en passer :
  `docker compose up -d db api web` et `Embeddings__Url` vide
  ([exploitation § Recherche](docs/reference/exploitation.md#recherche)).

## [0.1.0] — 2026-10-06

Première version publique.

### Ajouté

- Console orientée action, composée côté serveur selon le rôle (Admin, Gestionnaire,
  Utilisateur).
- Gestion des problèmes : priorité impact × urgence, analyses de cause racine (5 Pourquoi,
  Ishikawa 6M, arbre des défaillances), erreurs connues, actions correctives avec matrice
  RACI affectée à des utilisateurs ou groupes AD.
- Sept pratiques ITIL 4 en MVP : incidents, demandes, changements (avec calendrier),
  configuration (CI et relations), niveaux de service (catalogue et SLA), connaissances,
  amélioration continue ; liens entre enregistrements de tous les processus.
- Reporting : MTTR problèmes et incidents, taux de changements réussis, retards,
  volumétrie par processus.
- Identité Active Directory (LDAP → JWT), sans compte local ; thèmes clair et sombre.
- Mode démonstration : annuaire Samba AD et données d'exemple fictives, sans AD
  d'entreprise.
- Images publiées sur `ghcr.io` : `s-aloha-api`, `s-aloha-web`, `s-aloha-annuaire-demo`.
- Migrations EF, avec reprise des bases créées par les versions de développement.

### Mise à jour

- Première version : pas de mise à jour depuis une version publiée. Une base créée avant
  les migrations EF est reprise automatiquement
  ([ADR-0008](docs/decisions/adr-0008-migrations-ef.md)).
- Configuration par défaut destinée à l'évaluation : suivre la
  [liste de durcissement](docs/reference/securite.md#points-de-durcissement-avant-production)
  avant toute mise en production.

[Non publié]: https://github.com/christian-raj/S-Aloha/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/christian-raj/S-Aloha/releases/tag/v0.1.0
