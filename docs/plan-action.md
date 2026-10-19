# 🗺️ Plan d'action

<sub>[← Documentation](readme.md)</sub>

> **Ce qu'il reste à faire.** Alimenté par les constats ([`archives/`](archives/readme.md)),
> vidé au fur et à mesure : un chantier terminé quitte ce plan et rejoint le journal du
> jour ([protocole](protocole-documentation.md)).
>
> Le statut de chaque bug vit dans [GitHub Issues](https://github.com/christian-raj/S-Aloha/issues)
> ([ADR-0001](decisions/adr-0001-github-issues-source-de-verite.md)) ; ce plan porte les
> **chantiers** et l'ordre de traitement.
>
> *Mis à jour le 2026-10-06 : [constat initial](archives/etat-initial-2026-10-06.md),
> [revue fonctionnelle](archives/revue-fonctionnelle-2026-10-06.md).*

**Qui** débloque : *code* (réalisable sans arbitrage), *décision* (à trancher d'abord),
*action* (geste d'exploitation hors du dépôt).

## 1. Bloquants fonctionnels — le processus est inutilisable

Aucun bloquant ouvert : B1 à B3 corrigés et couverts par des tests le 2026-10-06
([journal](archives/journal-2026-10-06.md)).

## 2. Défauts majeurs — correctifs locaux

M1, M2, M4, M5 et M6 corrigés et couverts par des tests le 2026-10-06
([journal](archives/journal-2026-10-06.md)) ; M7 soldé le 2026-10-11 (PRB-24), M3 le
2026-10-12 (groupes AD dans le jeton, SOC-10, PRB-26). M8 attend une décision (section 4).

## 3. Règles métier que l'API ne tient pas

[Règles métier](reference/regles-metier.md) décrit le comportement **voulu** : c'est le
code qui doit s'y conformer.

| # | Écart | Qui |
|---|---|---|
| ~~R4~~ | ~~Réouverture d'un problème sans motif~~ — soldé le 2026-10-13 ([PRB-10](reference/processus/gestion-des-problemes.md#4-cycle-de-vie), motif exigé) | code |
| ~~R6~~ | ~~AD injoignable : 500 sur l'annuaire, « identifiants invalides » au login~~ — soldé le 2026-10-16 : 503 « Annuaire injoignable » à la connexion et à la recherche | code |
| ~~R7~~ | ~~Annuaire : aucun délai de connexion explicite~~ — soldé le 2026-10-16 : `Ldap:TimeoutSeconds` (5 s) sur la connexion et chaque opération ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 3) | code |

## 4. Chantiers à arbitrer

| # | Chantier | Décision à prendre |
|---|---|---|
| M8 | Aucune suppression dans l'interface | Qui supprime quoi, avec quelle confirmation |
| A2 | Réponses de l'API : entités EF exposées ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 5) | DTO de réponse par pratique, à l'occasion des évolutions, ou statu quo avec les champs internes masqués |
| O2 | Observabilité : ni journal structuré, ni traces, ni métriques ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 10) | Journal JSON et OpenTelemetry, selon la plateforme d'exploitation |
| A1 | **Console d'administration inexistante** (Admin = console Manager) | Décidé le 2026-10-07 : section de menu **Administration** (Admin : utilisateurs et rôles, santé, journal d'audit, nettoyage — [SOC-26](reference/regles-metier.md#9-traçabilité-commentaires-administration)) et section **Paramétrage** (Manager, Admin : référentiels — SOC-27 — et paramétrage des pratiques) ; voir [frontend § Navigation cible](reference/frontend.md#navigation-cible) |

## 5. Avant une mise en production

Détail : [sécurité § Points de durcissement](reference/securite.md#points-de-durcissement-avant-production).

| # | Chantier | Qui |
|---|---|---|
| S1 | **Secrets hors du dépôt** (`Jwt:Key`, mots de passe PostgreSQL et LDAP) | code + action |
| S2 | **LDAPS** et **HTTPS** | action |
| S3 | **CORS** restreint ([#122](https://github.com/christian-raj/S-Aloha/issues/122)) ; API et Swagger non publiés hors de nginx | code |
| S5 | Stockage du jeton côté navigateur (`sessionStorage` ou cookie `HttpOnly`) | décision |
| ~~S7~~ | ~~Refus de démarrer hors développement avec les valeurs d'exemple~~ — soldé le 2026-10-16 (`SampleSecrets` ; démonstration : `Security:AllowSampleSecrets`) ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 1) | code |
| ~~S8~~ | ~~Audience du jeton JWT~~ — soldé le 2026-10-16 (`Jwt:Audience`, émise et vérifiée) ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 2) | code |
| O1 | Démarrage : sondes `/health/live` et `/health/ready`, rattrapage des échéances SLA dans un service de fond, migrations pilotables (`Database:MigrateOnStartup`) ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 4) ; sondes : [#121](https://github.com/christian-raj/S-Aloha/issues/121) | code |

## 6. Qualité

| # | Chantier | Qui |
|---|---|---|
| Q1 | **Tests** : socle en place (API : xUnit + Testcontainers ; frontend : Vitest), B1–B3 et M1, M2, M4–M6 couverts ; étendre aux scénarios R1–R6 (R6 couvert le 2026-10-16) | code |
| Q2 | **CI** : build backend et frontend, `python3 scripts/check-docs.py` | code |
| Q4 | Créer les issues GitHub des sections 1 à 7 et les lier ici ; premières **good first issue** ouvertes le 2026-10-19 pour la communauté : [#120](https://github.com/christian-raj/S-Aloha/issues/120) à [#126](https://github.com/christian-raj/S-Aloha/issues/126) | action |
| Q5 | Mesure de la couverture de tests (coverlet) publiée en CI, sans seuil bloquant ; `dotnet format` ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 8) ; couverture : [#123](https://github.com/christian-raj/S-Aloha/issues/123) | code |
| Q6 | `.dockerignore` pour `backend` et `frontend` ([audit du 2026-10-16](archives/audit-code-2026-10-16.md) § 9) : [#120](https://github.com/christian-raj/S-Aloha/issues/120) | code |

## 7. Feuille de route fonctionnelle

Les dix pratiques tiennent leur cycle de vie complet ; toutes ont quitté le stade MVP le
2026-10-16 ([§ Sortie du MVP](#sortie-du-mvp), [cartographie](reference/produit.md#cartographie-des-processus-itil-et-état),
[ADR-0007](decisions/adr-0007-pratiques-itil4-et-socle-commun-des-processus.md)). Les évolutions
restantes sont spécifiées règle par règle dans [`reference/processus/`](reference/processus/readme.md)
(règles 🔜 des lots 2 et 3).

| # | Processus | Enrichissement | Règles |
|---|---|---|---|
| F2 | Incidents, Problèmes | Service et catégorie tirés du catalogue et d'un référentiel | [SOC-27](reference/regles-metier.md#9-traçabilité-commentaires-administration), [SLM-10](reference/processus/gestion-des-niveaux-de-service.md) |
| F3 | Demandes | Tâches d'exécution créées depuis le modèle (catalogue et modèles faits le 2026-10-14) | [REQ-24, REQ-25](reference/processus/gestion-des-demandes.md) |
| F5 | Configuration | Import en masse, attributs par type de CI, service signalé « dégradé » (rattachement CI ↔ service fait le 2026-10-13) ; exactitude de la CMDB ([#124](https://github.com/christian-raj/S-Aloha/issues/124)), CI à vérifier dans la console ([#125](https://github.com/christian-raj/S-Aloha/issues/125)) | [CFG-16, CFG-18, CFG-30](reference/processus/gestion-de-la-configuration.md) |
| F7 | Tous | Pièces jointes, notifications par courriel (notifications dans l'application et commentaires faits le 2026-10-13) | [SOC-22, SOC-25](reference/regles-metier.md#8-notifications) |
| F8 | Connaissances, Incidents | Suggestion d'articles et cas similaires dès la saisie, relier en un clic (rendu Markdown fait le 2026-10-12) | [INC-19](reference/processus/gestion-des-incidents.md), [RAG-09, RAG-11](reference/recherche.md) |
| F11 | Incidents, sécurité | **Déclarations réglementaires** des incidents cyber (NIS 2 : alerte précoce 24 h, notification 72 h, rapport final ; RGPD : 72 h) associées à l'incident, échéances et alertes | [INC-40 à INC-44](reference/processus/gestion-des-incidents.md), [DRG-01 à DRG-13](reference/processus/declarations-reglementaires.md) |
| F12 | Conformité | 🔜 **Évaluations sur référentiels définis par l'Admin** (ex. CR-CMM, ISO 27002) : l'Admin importe un référentiel en CSV depuis sa console en choisissant les colonnes (domaine ou pratique, niveau, identifiant, question, preuve attendue) et règle l'échelle (ex. Implémenté = 1, Partiel = 0,5, Non = 0 ; un niveau n'est atteint que si tous ses points de contrôle et ceux des niveaux inférieurs sont implémentés). Le contenu est importé par l'organisation et **jamais versionné** dans le dépôt, même logique que l'[ADR-0012](decisions/adr-0012-referentiel-nis2-structure-versionnee-texte-importe.md) : CR-CMM est sous licence CC BY-NC 4.0, usage interne libre, œuvre dérivée ou revente soumises à l'accord écrit de ses auteurs (vérifié le 2026-10-12). Place : section Conformité & réglementation | À spécifier |
| F13 | Configuration, Sécurité | **Découverte automatique du SI et CMDB augmentée** (phases 1 et 2, console de staging, Shadow IT et obsolescence faites le 2026-10-19 ; SNMP v3, SSH, WinRM, hyperviseurs, agent et preuves NIS 2 à venir) : approche hybride (agentless & agent léger), balayage IP / ICMP sweep, fingerprinting, synchronisation annuaire (AD / LDAP / Entra ID), cartographie des dépendances, protocoles standard (ICMP/ARP, SNMP v2c/v3, WMI/WinRM, SSH, REST), Credentials Vault, zone de staging et réconciliation, détection Shadow IT et alignement NIS 2 (ReCyF) | [Chantier F13](#découverte-automatique-et-cmdb-augmentée-f13) |

Ces chantiers sont décidés : leurs règles sont écrites et classées en lots
([plan d'implémentation du lot 1](reference/processus/readme.md#plan-dimplémentation-conseillé--lot-1)).

### Découverte automatique et CMDB augmentée (F13)

Ce chantier introduit le moteur de découverte automatique hybride pour alimenter la CMDB,
historiquement hors périmètre. Il s'appuie sur la sobriété (non-intrusivité, limitation de
bande passante), une zone de staging (tampon et réconciliation avant injection en production),
un coffre-fort d'identifiants chiffré (Credentials Vault), la cartographie des dépendances
et l'alignement NIS 2 (détection du Shadow IT et de l'obsolescence selon le ReCyF de l'ANSSI).
Modèle à 3 niveaux et architecture de la découverte : [§ 3.1](reference/processus/gestion-de-la-configuration.md#31-modèle-à-trois-couches) et [§ 4.2](reference/processus/gestion-de-la-configuration.md#42-découverte-et-sas-de-staging) de la pratique Configuration.

#### Principes directeurs

- **Non-intrusivité et sobriété** : scans asynchrones légers, limitation de bande passante (`SemaphoreSlim`, throttling configurable) pour préserver les équipements critiques et le réseau.
- **Approche hybride (Agent / Sans agent)** : priorité à l'agentless (ICMP, SNMP, WMI/WinRM, SSH, LDAP) pour un déploiement immédiat sans friction ; agent léger autonome pour les postes nomades ou hors réseau local.
- **Vision « Single Source of Truth » (SSOT)** : réconciliation centralisée rattachant directement les CI découverts aux incidents, problèmes, changements et services ITIL.
- **Traçabilité et conformité NIS 2** : identification du Shadow IT, détection des logiciels/OS obsolètes, et alimentation directe des preuves du Référentiel Cyber France (ReCyF ANSSI).

#### Plan de mise en œuvre par phases

| Phase | Intitulé | Description technique et livrables | Statut |
|---|---|---|---|
| **Phase 1** | **Socle de données, Credentials Vault & Directory Sync** | • Identification technique (IP, MAC, système), portefeuille applicatif (CFG-43, CFG-48) et actifs matériels (CFG-44) en colonnes dédiées ; CPU, RAM et autres attributs avec CFG-16.<br/>• Coffre d'identifiants chiffré AES-256-GCM, clé hors base (`DiscoveryCredentials` : comptes de service Windows, clés SSH, communautés SNMP).<br/>• Collecteur de l'annuaire, à part de `LdapService`, pour les ordinateurs du domaine (`objectCategory=computer`, recherche paginée).<br/>• Sas de staging (`DiscoveredAssets`) et moteur de réconciliation initiale (règles de matching par n° de série, MAC, FQDN). | ✅ (attributs génériques CFG-16 : lot 3) |
| **Phase 2** | **Découverte réseau & SNMP (Agentless)** | • Ordonnanceur de découverte (`DiscoveryJob` : plages IP / CIDR, fréquence et fenêtre hors heures de bureau).<br/>• Moteur de balayage réseau (Ping ICMP sweep, sondes TCP ciblées) et empreinte passive (OUI MAC, TTL, bannières HTTP/SSH).<br/>• Collecte SNMP v2c/v3 (MIB-II, `sysDescr`, `ifTable` pour commutateurs, routeurs, pare-feu, NAS, imprimantes). | ✅ (SNMP v3, OUI MAC, bannières, `ifTable` : 🔜) |
| **Phase 3** | **Inventaire approfondi & Cartographie des dépendances** | • Connecteurs d'administration authentifiés : SSH (Linux/Unix) et WMI/WinRM (Windows) pour inventaire complet (OS, logiciels installés, services actifs).<br/>• Connecteurs REST pour hyperviseurs et Cloud (VMware, Proxmox, AWS/Azure).<br/>• Cartographie automatique des dépendances (*Dependency Mapping*) : déduction des relations *« Héberge »* (hyperviseur $\rightarrow$ VM) et *« Se connecte à »* / *« Dépend de »* (analyse des flux et ports d'écoute actifs). | 🔜 |
| **Phase 4** | **Agent nomade, Alignement NIS 2 & Console de Staging** | • Agent léger autonome pour postes distants/nomades (`POST /api/discovery/agent-report` avec jeton d'enrôlement par périmètre).<br/>• Détection automatique du Shadow IT (actifs réseau non répertoriés) et de l'obsolescence (corrélation EOL) alimentant le module `ComplianceAssessment` (ReCyF Objectifs 4, 5, 6).<br/>• Interface utilisateur React : console de staging, tableau de réconciliation (diff avant/après) et validation par le Gestionnaire de configuration. | ✅ console de staging, Shadow IT, obsolescence, tableau de bord ; 🔜 agent, preuves NIS 2 |

**Lot 1** : terminé le 2026-10-13 (étapes 1 à 6, périmètres compris, NIS-18), sauf RAG-10
(calibrage de la recherche), **en attente** d'un jeu de requêtes réelles (décision du
2026-10-13). **Itération en cours** : règles 🔜 du lot 2, et
[déclarations réglementaires](reference/processus/declarations-reglementaires.md) (F11).

### Sortie du MVP

**Toutes les pratiques sont sorties du MVP le 2026-10-16.** Une pratique perdait la mention
**(MVP)** quand les prérequis communs étaient en place — lot 1, commentaires (SOC-24) et
notifications (SOC-21), tous faits le 2026-10-13 — et que toutes ses règles de sortie
(cœur de métier du lot 2) étaient à ✅ :

| Date | Pratiques | Réserves acceptées |
|---|---|---|
| — | Problèmes (pratique de référence, jamais en MVP ; dernières règles PRB-10, PRB-25, PRB-27 le 2026-10-13) | |
| 2026-10-13 | Connaissances, Configuration, Conformité NIS 2 | RAG-10 en attente ; pièces jointes de NIS-08 avec SOC-25 |
| 2026-10-14 | Niveaux de service, Amélioration continue, Incidents, Demandes | SLM-21 et tâches (REQ-24, REQ-25) au lot 3 ; modèle de demande facultatif ; cible de conformité d'un SLA facultative |
| 2026-10-16 | Changements, Projets | CI concernés d'un modèle standard à venir |

Le détail règle par règle est dans les journaux du
[2026-10-13](archives/journal-2026-10-13.md), du [2026-10-14](archives/journal-2026-10-14.md)
et du [2026-10-16](archives/journal-2026-10-16.md).

## 8. Communauté et financement

Accueil des contributeurs et financement du projet ; canaux et contreparties publics :
[Soutenir S-Aloha](soutenir.md).

| # | Chantier | Qui |
|---|---|---|
| C2 | Code de conduite (attend l'adresse de contact dédiée), `.github/FUNDING.yml` (après C6) | code + action |
| C5 | Tableau GitHub Projects public alimenté par ce plan (le jeton `gh` du mainteneur doit recevoir le droit `project` : `gh auth refresh -s project`) | action |
| C6 | **Activer GitHub Sponsors** (profil du mainteneur : informations bancaires et fiscales), niveaux et contreparties ([ADR-0011](decisions/adr-0011-financement-sponsoring-et-services.md)) | action |
| C7 | Lancement : LinuxFr, Reddit, AlternativeTo, Framalibre (textes prêts) ; téléverser l'image de partage `docs/screenshots/partage-1280x640.png` (Settings → Social preview) ; awesome-selfhosted **à partir du 2027-02-06** (première version de plus de 4 mois exigée) | action |
| C8 | Démo en ligne réinitialisée chaque nuit | décision |
| C9 | `GOVERNANCE.md` dès le deuxième contributeur régulier | décision |
