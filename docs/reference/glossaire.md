# 📖 Glossaire

> Les termes ITIL, S-Aloha, techniques et malgaches employés dans l'interface et la documentation.

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## ITIL et processus

| Terme | Définition |
|---|---|
| **ITIL** | Référentiel de bonnes pratiques de gestion des services IT (PeopleCert/Axelos). Les processus de S-Aloha reprennent les objectifs des **pratiques ITIL 4** ([ADR-0007](../decisions/adr-0007-pratiques-itil4-et-socle-commun-des-processus.md)). |
| **Pratique** (ITIL 4) | Ensemble de ressources organisationnelles destiné à accomplir un objectif (gestion des incidents, habilitation des changements…). Dans S-Aloha : un processus, un module. |
| **Processus** | Ensemble d'activités ITIL (incidents, problèmes, changements…). Un processus = un module S-Aloha. |
| **Incident** | Interruption ou dégradation d'un service ; l'objectif est de le rétablir au plus vite. |
| **Incident majeur** | Incident d'impact exceptionnel, suivi en tête de la console des gestionnaires jusqu'à sa résolution. |
| **Demande de service** | Demande prédéfinie d'un utilisateur (accès, matériel, information), approuvée puis satisfaite. |
| **Problème** | Cause, inconnue au départ, d'un ou plusieurs incidents. On en cherche la cause racine. |
| **Erreur connue** (*Known Error*) | Problème dont la cause racine est identifiée et pour lequel un **contournement** est documenté. |
| **Contournement** (*workaround*) | Moyen de réduire ou supprimer l'impact d'un problème en attendant sa résolution définitive. |
| **RCA** (*Root Cause Analysis*) | Analyse de cause racine. Trois méthodes dans S-Aloha : 5 Pourquoi, Ishikawa, FTA. |
| **5 Pourquoi** | Chaîne de questions « pourquoi ? » successives jusqu'à la cause racine. |
| **Ishikawa (6M)** | Diagramme en arête de poisson ; causes classées en six familles : Méthode, Matériel, Main-d'œuvre, Milieu, Matière, Mesure. |
| **FTA** (*Fault Tree Analysis*) | Arbre des défaillances : décomposition d'un événement redouté en causes combinées par des portes **ET** / **OU**. |
| **RACI** | Matrice d'affectation : **R**esponsable (réalise), **A**pprobateur (rend compte, un seul), **C**onsulté, **I**nformé. |
| **Impact × urgence** | Les deux axes qui déterminent la **priorité** P1 (la plus haute) à P4. |
| **MTTR** | *Mean Time To Resolve* : durée moyenne de résolution — en jours pour les problèmes (création → clôture), en heures pour les incidents (création → résolution). |
| **Changement** | Ajout, modification ou retrait de tout ce qui peut affecter un service. Types : **standard** (modèle déjà évalué, pré-autorisé), **normal** (évalué et autorisé), **urgent** (à mettre en œuvre au plus vite). |
| **Habilitation des changements** (*change enablement*) | Pratique ITIL 4 : évaluer les risques, autoriser les changements, gérer leur calendrier. |
| **Autorité de changement** | Personne ou instance qui autorise un changement ; dans S-Aloha, un gestionnaire. |
| **Calendrier des changements** | Planning des changements planifiés (*change schedule*). |
| **Plan de retour arrière** (*backout plan*) | Procédure de retour à l'état antérieur si un changement échoue. |
| **CI** (*Configuration Item*) | Élément de configuration : composant à gérer pour délivrer un service (serveur, application, base…), relié à d'autres CI. |
| **CMDB** | Base de données de configuration : CI et leurs relations (processus Configuration). |
| **Catalogue des services** | Liste des services délivrés aux métiers, avec leur criticité et leurs heures de service. |
| **SLA** | *Service Level Agreement* : accord de niveau de service entre le fournisseur et un client, avec des cibles (disponibilité, délais de résolution). |
| **Article de connaissance** | Solution, procédure, erreur connue documentée ou FAQ, publié après validation et revu périodiquement. |
| **CSI** | *Continual Service Improvement* : amélioration continue des services (pratique *continual improvement* d'ITIL 4). |
| **Registre d'amélioration continue** (CIR) | Liste des opportunités d'amélioration, avec leur valeur attendue, leur priorité et leur avancement. |
| **Modèle d'amélioration continue** | Démarche ITIL 4 en sept étapes : vision, situation actuelle, cible, plan, action, vérification, maintien de la dynamique. |
| **Référence** | Identifiant d'un enregistrement : préfixe du processus, année, numéro (`INC-2026-0001`). Sert aussi à relier deux enregistrements. |

## S-Aloha

| Terme | Définition |
|---|---|
| **Pilier** | L'un des six axes S-A-L-O-H-A (Service, Alignment, Leadership, Optimisation, Harmonie, Agilité) auquel chaque processus est rattaché. |
| **Socle** (*Core*) | Code transverse à tous les processus : authentification, annuaire, données, pilotage. |
| **Module** | Implémentation d'un processus ITIL (`Modules/<Processus>`, `modules/<processus>`). |
| **Registre** | `frontend/src/modules/registry.js`, source unique de la navigation : piliers et processus, avec leur état. |
| **Bientôt** | État d'un processus déclaré dans le registre mais sans interface (`status: 'soon'`). |
| **Pilotage** | Section transverse de la navigation : Ma console et Reporting. |
| **Ma console** | Page d'accueil par rôle, orientée action : zones « À traiter » et « À suivre ». |

## Technique

| Terme | Définition |
|---|---|
| **AD** | Active Directory, annuaire de l'organisation : source des identités et des rôles. |
| **LDAP / LDAPS** | Protocole d'accès à l'annuaire ; LDAPS est sa version chiffrée (port 636). |
| **Bind** | Authentification auprès de l'annuaire LDAP (compte de service, ou utilisateur pour valider son mot de passe). |
| **`sAMAccountName`** | Identifiant de connexion Windows d'un utilisateur AD ; sert d'identifiant dans S-Aloha. |
| **JWT** | Jeton signé émis à la connexion, porteur du nom et du rôle, envoyé dans l'en-tête `Authorization: Bearer`. |
| **Policy** | Règle d'autorisation ASP.NET Core : `User`, `Manager`, `Admin`. |
| **Jeton de design** | Variable CSS nommée (`--bg-surface`, `--accent-blue`…) redéfinie par thème. |
| **ADR** | *Architecture Decision Record* : une décision structurante par fichier, dans [`decisions/`](../decisions/). |

## Vocabulaire malgache

| Terme | Sens |
|---|---|
| **Aloha** | « D'abord, avant » — le service passe avant tout. |
| **Service alohan'ny zavatra rehetra** | « Le service avant toute chose » — devise de la page de connexion. |
| **Ny fahaiza-manao ho amin'ny tolotra tsara kokoa** | « Le savoir-faire au service d'une meilleure offre » — maxime de la page de connexion. |
