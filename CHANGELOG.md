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

### Modifié

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
