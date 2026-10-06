# 📖 Glossaire

> Les termes ITIL, S-Aloha, techniques et malgaches employés dans l'interface et la documentation.

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## ITIL et gestion des problèmes

| Terme | Définition |
|---|---|
| **ITIL** | Référentiel de bonnes pratiques de gestion des services IT. S-Aloha suit le découpage en processus d'ITIL v3. |
| **Processus** | Ensemble d'activités ITIL (incidents, problèmes, changements…). Un processus = un module S-Aloha. |
| **Incident** | Interruption ou dégradation d'un service ; l'objectif est de le rétablir au plus vite. |
| **Problème** | Cause, inconnue au départ, d'un ou plusieurs incidents. On en cherche la cause racine. |
| **Erreur connue** (*Known Error*) | Problème dont la cause racine est identifiée et pour lequel un **contournement** est documenté. |
| **Contournement** (*workaround*) | Moyen de réduire ou supprimer l'impact d'un problème en attendant sa résolution définitive. |
| **RCA** (*Root Cause Analysis*) | Analyse de cause racine. Trois méthodes dans S-Aloha : 5 Pourquoi, Ishikawa, FTA. |
| **5 Pourquoi** | Chaîne de questions « pourquoi ? » successives jusqu'à la cause racine. |
| **Ishikawa (6M)** | Diagramme en arête de poisson ; causes classées en six familles : Méthode, Matériel, Main-d'œuvre, Milieu, Matière, Mesure. |
| **FTA** (*Fault Tree Analysis*) | Arbre des défaillances : décomposition d'un événement redouté en causes combinées par des portes **ET** / **OU**. |
| **RACI** | Matrice d'affectation : **R**esponsable (réalise), **A**pprobateur (rend compte, un seul), **C**onsulté, **I**nformé. |
| **Impact × urgence** | Les deux axes qui déterminent la **priorité** P1 (la plus haute) à P4. |
| **MTTR** | *Mean Time To Resolve* : durée moyenne entre création et clôture d'un problème, en jours. |
| **CMDB** | Base de données de configuration : actifs IT et leurs dépendances (processus Configuration, à venir). |
| **SLA** | *Service Level Agreement* : engagement de niveau de service (processus Niveaux de service, à venir). |
| **CSI** | *Continual Service Improvement* : amélioration continue des services. |

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
