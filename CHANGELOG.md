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

- **Administration › Santé et Journal d'audit** (ADM-20 à ADM-31) : état de la base, de
  l'annuaire, des embeddings, du coffre et des tâches de fond, version déployée ; journal
  d'audit de toutes les pratiques, filtrable par auteur, pratique, action, référence et
  période, exportable en CSV.

- **Observabilité** (O2) : journal JSON hors développement, identifiant de trace sur chaque
  ligne et dans l'en-tête `traceparent` des réponses ; traces et métriques OpenTelemetry
  (HTTP, PostgreSQL, annuaire, découverte, notifications) exportées si
  `OTEL_EXPORTER_OTLP_ENDPOINT` est défini.
- **Suppressions dans l'interface** (SOC-14, M8) : un Admin supprime un enregistrement, un
  problème compris, et un gestionnaire une analyse ou une action corrective, après une
  confirmation qui rappelle la référence et compte ce qui part avec (liens, commentaires,
  analyses, relations, accords…).

### Modifié

- **Réponses de l'API en DTO** (A2) : l'API ne renvoie plus ses entités internes, mais des
  objets de réponse explicites, de mêmes noms de champs ; les navigations internes (problème
  d'une analyse, données brutes de la découverte…) n'en sortent plus. Le contrat est figé par
  un test.

### Corrigé

- **Fins de support** : la liste (`GET /api/discovery/end-of-life`) est de nouveau lisible par
  tous les utilisateurs, comme documenté ; elle exigeait le rôle Manager.

### Sécurité

- **Jeton de session en cookie HttpOnly** (S5) : le jeton n'est plus lisible par les scripts
  de la page ; cookie `SameSite=Strict`, `Secure` en HTTPS, protection CSRF par l'en-tête
  `X-S-Aloha`, déconnexion côté serveur ; nginx sert une politique de sécurité du contenu
  (CSP) et `X-Content-Type-Options`.

### Mise à jour

- **Session (S5)** : chacun se reconnecte une fois. Un script qui appelait
  `POST /api/auth/login` et lisait `token` dans la réponse ajoute `"returnToken": true` à sa
  requête. Derrière un reverse proxy TLS, transmettre `X-Forwarded-Proto` pour un cookie
  `Secure`.

## [0.2.0] — 2026-10-19

Toutes les pratiques sortent du stade MVP ; la configuration gagne le portefeuille
applicatif, les actifs matériels et la découverte automatique du réseau ; l'API se durcit
(secrets d'exemple refusés, audience du jeton, annuaire injoignable signalé). **Lire la
section Mise à jour avant d'installer** : migrations de base, nouveaux secrets à fournir.

### Ajouté

- **CMDB augmentée et découverte automatique** (chantier F13) : la fiche d'un CI porte son
  identification technique (IP, MAC, système), le **portefeuille applicatif** (code,
  direction, propriétaire métier, responsable DSI, criticité, RTO / RPO, RGPD, NIS 2, cycle
  applicatif) et les **actifs matériels** (constructeur, modèle, numéro de série, garantie,
  fin de support). **Découverte** par balayage IP, SNMP v2c et annuaire Active Directory,
  planifiée par tâche (fréquence, fenêtre horaire) ; les équipements trouvés attendent dans
  un **sas de staging** où ils sont rapprochés des CI (numéro de série, MAC, nom d'hôte, IP),
  créés en CI ou ignorés ; actifs inconnus (Shadow IT) signalés. **Coffre d'identifiants**
  chiffré (communautés SNMP, comptes de domaine), **référentiel des fins de support**,
  **rapport d'inventaire** exportable, onglet **Configuration & inventaire** du tableau de
  bord, bloc du sas dans la console des gestionnaires (CFG-39 à CFG-48, REP-50 à REP-53).

- **Projets sort du MVP**, dernière pratique : PRJ-22 (prévision de la date de fin) et PRJ-34
  (console) sont en place. **Toutes les pratiques ont quitté le stade MVP** ; la mention
  disparaît du README et de la cartographie.

- **Projets** : **date de fin prévue** (vélocité des sprints, phases planifiées ou avancement),
  comparée à la tolérance de délai (PRJ-22) ; **console** : projets à valider, projets hors
  tolérance ou à jalons en retard à relancer, mes projets (PRJ-34). La console affiche aussi
  les demandes en retard, que l'API fournissait déjà (REQ-30).

- **Changements sort du MVP** : toutes les règles listées pour sa sortie sont en place
  (CHG-14 à CHG-16, CHG-20, CHG-21, CHG-24 à CHG-26) ; la mention **(MVP)** disparaît du
  README et de la cartographie. Un changement Standard exige un modèle standard ; les CI
  concernés d'un modèle restent à venir.

- **Gouvernance des changements** : un changement normal à risque élevé est autorisé par le
  **CAB** — deux gestionnaires distincts approuvent, un rejet motivé suffit (CHG-20) ; **périodes
  de gel** sur le calendrier, où seul un changement urgent se planifie (CHG-25) ; **modèles de
  changement standard** : un changement Standard se crée depuis un modèle actif, dont il reprend
  les plans et le risque (CHG-26). Migration `ChangeCabFreezeModels`.

- **Changements** : dates réelles exigées pour la mise en œuvre et la clôture, dépassement de
  la fin planifiée signalé (CHG-14) ; revue post-implémentation exigée pour clore un changement
  urgent ou échoué (CHG-15) ; un changement normal ou urgent s'évalue avec au moins un CI
  relié (CHG-16) ; le demandeur n'autorise pas son propre changement, sauf l'Admin (CHG-21).
  Migration `ChangeActualDatesReview`.

- **Demandes sort du MVP** : toutes les règles listées pour sa sortie sont en place (REQ-09,
  REQ-20 à REQ-23, REQ-30, REQ-31) ; la mention **(MVP)** disparaît du README et de la
  cartographie. Le modèle reste facultatif (demande libre possible) ; les tâches (REQ-24,
  REQ-25) restent au lot 3.

- **Catalogue de demandes** (REQ-20) : les gestionnaires tiennent des modèles (objet, service,
  approbation requise, délai cible, groupe d'exécution) ; une demande se crée depuis un modèle,
  qui en donne l'objet et le service. Un modèle **pré-approuvé** crée la demande Approuvée
  (REQ-21) ; l'**échéance** est calculée à l'approbation en heures de service et seul un
  gestionnaire la modifie (REQ-22) ; le **groupe d'exécution** devient responsable (REQ-23).
  **Demandes en retard** marquées, relancées et listées dans la console (REQ-30). Migration
  `RequestCatalog`.

- **Incidents sort du MVP** : toutes les règles listées pour sa sortie sont en place (INC-07,
  INC-11, INC-13, INC-20 à INC-22, INC-24, INC-25) ; la mention **(MVP)** disparaît du README
  et de la cartographie.

- **Incidents** : le responsable d'un incident majeur doit être gestionnaire du périmètre de
  l'incident, vérifié dans l'annuaire (INC-13) ; **état SLA** de chaque incident (en risque,
  dépassé, suspendu, respecté…) sur la fiche et dans le registre, et incidents en risque ou
  hors délai dans le groupe Urgent de la console du responsable et des gestionnaires (INC-24).

- **Niveaux de service et Amélioration continue sortent du MVP** : toutes les règles listées
  pour leur sortie sont en place (SLM-04, SLM-10, SLM-13, SLM-14, SLM-20, SLM-22 à SLM-24 ;
  CSI-10, CSI-12, CSI-20, CSI-21, CSI-23) ; la mention **(MVP)** disparaît du README et de
  la cartographie. Le respect d'un SLA est mesuré ; sans cible de conformité renseignée, il
  ne déclenche pas d'alerte. La disponibilité mesurée (SLM-21) reste au lot 3.

- **Respect des SLA mesuré** (SLM-20) : part mensuelle des incidents du service résolus dans
  le délai, par priorité, sur la fiche du SLA et par `GET /api/agreements/{id}/compliance` ;
  **cible de respect** facultative sur le SLA. Un mois achevé sous la cible **alerte** les
  gestionnaires et **propose une amélioration** reliée au SLA (SLM-23, CSI-20). Migration
  `AgreementComplianceTarget`.

- **Heures de service** : chaque service porte ses jours ouvrés, sa plage horaire, ses jours
  fériés et son fuseau ; les échéances SLA des incidents s'y comptent (SLM-14, INC-22).
  **Attente motivée** : passer un incident En attente exige un motif (utilisateur,
  fournisseur, changement) ; l'horloge SLA s'arrête pendant l'attente et les échéances en
  sont prolongées à la reprise (INC-07, INC-22). Migration `ServiceHoursOnHold`.

- **Service du catalogue** sur les incidents, problèmes et demandes : choisi dans une liste au
  lieu d'un texte libre, son nom recopié pour l'affichage et le reporting ; l'échéance SLA de
  l'incident suit ce service (SLM-10, INC-20) ; un service retiré ne se choisit plus pour un
  nouvel enregistrement ni un nouveau SLA (SLM-15). Migration `ServiceCatalogLinks` : les
  anciens libellés égaux au nom d'un service du catalogue y sont rattachés.

- **Niveaux de service** : un SLA dont la validité est passée **expire** de lui-même, les
  gestionnaires sont prévenus (SLM-04) ; **délais de prise en charge** P1 à P4 dans le SLA et
  échéance de prise en charge sur l'incident (SLM-13) ; **revue de SLA** : constats et
  décisions enregistrés, prochaine revue reportée de 12 mois (SLM-22). Migration
  `AgreementReviewResponse`.

- **Conformité NIS 2 sort du MVP** : toutes les règles listées pour sa sortie sont en place
  (NIS-08, NIS-09, NIS-14, NIS-16, NIS-17, NIS-18) ; la mention **(MVP)** disparaît du README
  et de la cartographie. Les preuves se relient aux enregistrements ; les pièces jointes
  suivront avec SOC-25.

- **Conformité NIS 2 — preuves et comparaison** : chaque réponse porte ses **preuves**,
  enregistrements désignés par leur référence (changement, article, CI…), reprises dans
  l'export (NIS-08 ; pièces jointes à venir avec SOC-25) ; onglet **Comparaison** avec une
  autre évaluation du même périmètre : évolution des scores par pilier, objectif et
  thématique, exigences dont la réponse a changé (NIS-09). Migration `AssessmentEvidence`
  (nouvelle table).

- **Conformité NIS 2** : la synthèse montre pour chaque écart l'action d'amélioration liée et
  son statut (NIS-14) ; date de la prochaine évaluation (12 mois après la validation par
  défaut), relance du pilote à l'échéance et entrée « À renouveler » dans la console des
  gestionnaires (NIS-16) ; **export CSV de la synthèse** pour les auditeurs (NIS-17).
  Migration `AssessmentNextDate` (une colonne nulle).

- **Problèmes** : rouvrir un problème résolu ou clos exige un motif (PRB-10) ; une action
  corrective n'est déclarée terminée que par son approbateur (A) ou un gestionnaire, son
  responsable (R) demande l'approbation, l'A est notifié (PRB-25) ; **créer un changement
  depuis une action** : titre et description repris, changement rattaché à l'action et relié
  au problème (PRB-27, CHG-23). Migration `ProblemReopenActionApproval` (colonnes nulles).

- **Configuration sort du MVP** : toutes les règles listées pour sa sortie sont en place
  (CFG-12 à CFG-15, CFG-20 à CFG-22) ; la mention **(MVP)** disparaît du README et de la
  cartographie.

- **Configuration** : vérification d'un CI par son propriétaire ou un gestionnaire, relance
  au propriétaire d'un CI En service non vérifié depuis 12 mois (CFG-15) ; services touchés
  par un CI et par ses CI en aval (CFG-21) ; onglet **Impact** de la fiche d'un changement
  (CFG-20) ; CI passé Hors service notifié aussi aux responsables des services touchés
  (CFG-22). Migration `CiVerification` (deux colonnes nulles).

- **Amélioration continue** : source de chaque amélioration et enregistrement d'origine
  relié (CSI-10) ; valeur et effort notés à la validation, priorité calculée (CSI-12) ;
  amélioration en retard relancée à son porteur (CSI-21) ; **récurrence après correctif** :
  un nouvel incident relié à un problème résolu ou clos alerte les gestionnaires et propose
  une amélioration reliée (PRB-23, CSI-20).

- **Connaissances sort du MVP** : toutes les règles listées pour sa sortie sont en place
  (KB-10, KB-11, KB-12, KB-17, KB-20, KB-21) ; la mention **(MVP)** disparaît du README et
  de la cartographie. RAG-10 (calibrage de la recherche), en attente d'un jeu de requêtes
  réelles, ne la conditionne pas (décision du 2026-10-13).

- **Notifications datées** (SOC-21) : relevé périodique (15 minutes, `Notifications:IntervalMinutes`) — SLA en risque → responsable ; SLA dépassé et incident P1 → responsable et gestionnaires (escalade, INC-18) ; veille d'une mise en œuvre → responsable du changement ; action corrective en retard → R, A et gestionnaires ; revue d'article échue → responsable ; SLA expirant dans 30 jours → gestionnaires, revue de SLA échue → propriétaire du service. Un même événement n'est notifié qu'une fois.

- **Notifications dans l'application** (SOC-21, SOC-23) : cloche avec compteur des non lues ;
  toute affectation notifie le nouveau responsable (ou son groupe) ; décisions attendues →
  gestionnaires du périmètre (demande soumise, changement évalué, amélioration proposée,
  article soumis, incident majeur) ; décisions rendues → demandeur (approbation, rejet,
  autorisation, publication, validation, problème qualifié ou clos, incident résolu) ;
  action corrective affectée → son R et son A. Jamais pour un enregistrement que le
  destinataire ne voit pas, jamais à l'auteur de l'événement.

- **Commentaires** (SOC-24) : onglet « Commentaires » sur chaque fiche ; un commentaire est
  public (lu par le demandeur) ou note de travail (lue et écrite par ceux qui traitent
  l'enregistrement, jamais par le demandeur qui n'est pas intervenant) ; retrait par l'auteur
  ou un Admin.

- **Conformité NIS 2 — version du référentiel** (NIS-18) : chaque évaluation enregistre la
  version du ReCyF évaluée ; à la validation, ses exigences applicables sont figées, et une
  nouvelle version embarquée ne change ni son questionnaire, ni son score, ni la synthèse du
  reporting ; la rouvrir la réaligne sur la version en vigueur.

- **Administration › Périmètres** (Admin) : liste des périmètres (supprimés grisés et
  restaurables), création, fiche avec renommage, enregistrements rattachés par pratique,
  affectations d'utilisateurs ou de groupes AD choisis dans l'annuaire (Manager ou User),
  suppression douce et suppression définitive quand rien n'y est rattaché (PER-01, PER-02,
  PER-12). L'étape 6 du lot 1 est terminée.
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

- **Passage à .NET 10 (LTS)** : API en `net10.0`, EF Core 10, Npgsql EF 10, JwtBearer 10,
  Swashbuckle 10 (OpenAPI 3), client LDAP 4 (asynchrone) ; images Docker `sdk:10.0` et
  `aspnet:10.0`. Frontend en **React 19**, Vite 8, Vitest 5. .NET 11 n'est encore qu'en
  version candidate : il sera adopté à sa sortie stable.

- **Recherche d'un compte dans l'annuaire** : les renvois (*referrals*) d'Active Directory sont
  ignorés ; un compte inconnu est refusé comme responsable d'un incident majeur, et la
  connexion ne journalise plus de faux échec LDAP.

- **Sous-titres des pages** : le numéro de version d'ITIL est retiré (« (ITIL) ») ; la page
  Problèmes affichait « ITIL v3 » alors que l'application suit les pratiques ITIL 4.
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

### Sécurité

- **Secrets d'exemple refusés** (S7) : hors développement, l'API refuse de démarrer avec la
  clé JWT, la clé du coffre de la découverte, le mot de passe du compte de service ou celui de
  la base livrés en exemple, ou avec une clé JWT de moins de 64 caractères ; le mode démonstration l'accepte explicitement
  (`Security:AllowSampleSecrets`) et le journalise.
- **Audience du jeton** (S8) : les jetons portent et doivent porter l'audience `S-Aloha`
  (`Jwt:Audience`).

### Corrigé

- **Annuaire injoignable** (R6) : la connexion répond « Annuaire injoignable » (503) au lieu
  de « identifiants invalides », et la recherche de personnes et de groupes répond 503 au lieu
  d'une erreur 500. Un contrôleur de domaine qui ne répond pas est abandonné après
  `Ldap:TimeoutSeconds` (5 s par défaut) au lieu du délai TCP du système (R7).

### Mise à jour

- **Secrets** : une installation hors démonstration qui garde la clé JWT, la clé du coffre
  (`Discovery:VaultKey`), le mot de passe de base ou celui du compte de service d'exemple ne
  démarre plus (S7) : les remplacer avant de
  mettre à jour. Les jetons émis avant la mise à jour n'ont pas d'audience : chacun se reconnecte
  une fois (S8).
- **.NET 10** : reconstruire les images (`docker compose build`) ; aucune migration de base
  n'accompagne ce passage. En développement : SDK .NET 10 et `dotnet-ef` 10
  (`dotnet tool update -g dotnet-ef --version 10.*`) ; `DOTNET_ROLL_FORWARD` n'est plus utile.
- `docker compose up` démarre désormais le service `embeddings` et télécharge le modèle
  `bge-m3` (environ 1,2 Go) au premier démarrage ; pour s'en passer :
  `docker compose up -d db api web` et `Embeddings__Url` vide
  ([exploitation § Recherche](docs/reference/exploitation.md#recherche)).
- **Découverte (CFG-45)** : définir `Discovery__VaultKey` (32 caractères au moins, hors du
  dépôt) **avant** d'enregistrer un premier identifiant, puis ne plus la changer : les
  secrets déjà chiffrés deviendraient illisibles. La migration `CmdbDiscovery` s'applique au
  démarrage. Le balayage IP a besoin que l'API joigne les réseaux visés (ping et ports TCP ;
  un conteneur sans ICMP s'en tient aux ports).
- **Portefeuille applicatif et actifs matériels (CFG-43, CFG-44)** : une application En
  service ou Hors service doit désormais porter son code, sa direction, son duo de
  gouvernance et sa criticité ; un équipement réseau, de stockage ou un poste, son
  constructeur, son numéro de série et son emplacement. Les CI existants restent lisibles,
  mais leur prochaine modification exigera ces champs : les compléter depuis l'inventaire
  (filtre « Gouvernance incomplète »).

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

[Non publié]: https://github.com/christian-raj/S-Aloha/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/christian-raj/S-Aloha/releases/tag/v0.2.0
[0.1.0]: https://github.com/christian-raj/S-Aloha/releases/tag/v0.1.0
