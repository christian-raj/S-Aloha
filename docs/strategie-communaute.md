# 🌱 Stratégie communauté et financement

<sub>[← Documentation](readme.md) · [Plan d'action](plan-action.md)</sub>

> Comment faire de S-Aloha un projet que des développeurs rejoignent et que des
> organisations financent. Les chantiers qui en découlent vivent dans le
> [plan d'action § 8](plan-action.md#8-communauté-et-financement) ; les choix tranchés
> deviendront des ADR.
>
> *Établie le 2026-10-06, à revoir tous les trimestres.*

## Où en est le projet

### Au départ (2026-10-06, matin)

| Atout | Frein |
|---|---|
| 8 processus ITIL 4 en MVP, socle modulaire documenté | **Impossible à essayer sans Active Directory** |
| Licence AGPLv3, CI, tests, `CONTRIBUTING`, `SECURITY`, modèles d'issues | Aucune version publiée, aucune image Docker |
| Documentation soignée, ADR, wiki, captures d'écran | 0 étoile, aucune issue pour un nouveau venu |
| Stack répandue (.NET 8, React, PostgreSQL) | Pas de démo, pas de Discussions, pas de code de conduite |
| Cause racine outillée, console orientée action | Projet porté par une seule personne |

### Avancement (2026-10-06, soir)

| Frein | État |
|---|---|
| Essai sans AD | ✅ Mode démonstration avec annuaire et données d'exemple : application prête en 28 s depuis un clone neuf |
| Version et images | ✅ [v0.1.0](https://github.com/christian-raj/S-Aloha/releases/tag/v0.1.0), images publiques sur `ghcr.io`, [journal des versions](../CHANGELOG.md) |
| Issues pour nouveaux venus | ✅ 8 issues rédigées (#23–#30), Discussions activées |
| Contributions | ✅ DCO vérifié par la CI, README bilingue |
| Code de conduite, Sponsors, tableau de feuille de route | ⏳ En attente du mainteneur (plan C2, C5, C6) |
| Visibilité | ⏳ Textes de lancement prêts, image de partage prête (`docs/screenshots/partage-1280x640.png`) ; awesome-selfhosted à partir du **2027-02-06** (première version de plus de 4 mois exigée) |
| Plus d'un mainteneur | ⏳ Dépend des premières contributions |

Indicateurs au 2026-10-06 : 1 étoile, 0 fork, profil communautaire GitHub à 85 %,
premier commentaire extérieur reçu (issues #29 et #30).

## Positionnement

Le marché des ITSM libres a des acteurs installés, et un développeur comme un sponsor
demandera d'abord **« pourquoi pas eux ? »**. GLPI et iTop, deux projets français sous
licence libre, couvrent l'inventaire, l'assistance et la CMDB depuis des années. S-Aloha
ne gagne pas sur l'étendue fonctionnelle : il gagne sur trois points, à marteler partout.

1. **Prêt en une commande, branché sur l'AD** : aucun compte à créer, les rôles suivent les
   groupes de l'annuaire.
2. **Orienté action** : chacun arrive sur ce qui requiert son intervention, pas sur un
   tableau de bord à interpréter.
3. **ITIL 4 et cause racine** : les pratiques ITIL 4 telles qu'elles sont publiées, et
   une gestion des problèmes outillée (5 Pourquoi, Ishikawa, arbre des défaillances) que
   les outils généralistes traitent rarement.

**Pour qui** : la DSI de taille moyenne, francophone d'abord (France, Belgique, Suisse,
Québec, Afrique francophone, Madagascar), équipée d'un Active Directory, qui veut un
outil ITIL simple, hébergé chez elle, sans licence par utilisateur.

## Axe 1 — Rendre le projet essayable en cinq minutes

C'est le préalable à tout le reste : un projet qu'on ne peut pas lancer n'attire ni
contributeur ni sponsor.

- **Mode démonstration** : un profil `docker compose --profile demo up` qui démarre un
  annuaire de démonstration (contrôleur Samba AD) avec trois comptes, un par rôle, et des
  données d'exemple. Le principe existe déjà pour le déploiement d'évaluation local ; il
  reste à le rendre public et générique.
- **Images publiées** sur le registre GitHub (`ghcr.io`) à chaque version : plus besoin de
  compiler pour essayer.
- **Versions numérotées** (`v0.1.0`, puis SemVer) avec notes de version : un projet qui
  publie des versions inspire confiance.
- **Démo en ligne**, réinitialisée chaque nuit, une fois le mode démonstration en place
  (coût d'hébergement : à couvrir par les premiers sponsors).

## Axe 2 — Accueillir les contributeurs

- **Issues prêtes à prendre** : 8 à 10 issues `good first issue` / `help wanted`
  rédigées avec contexte, fichiers concernés et critère de fin, tirées du plan d'action
  (validations R1–R3, message d'erreur AD injoignable R6, rendu Markdown des articles F8…).
  Un nouveau venu choisit une issue en une minute.
- **Feuille de route publique** : un tableau GitHub Projects alimenté par le plan
  d'action, pour qu'on voie ce qui arrive et où aider.
- **GitHub Discussions** : questions, idées, présentations d'usages — les issues restent
  réservées aux bugs et aux évolutions qualifiées.
- **Code de conduite** (Contributor Covenant, en français) : dernier fichier manquant au
  profil communautaire GitHub.
- **Ouverture à l'anglais** : la documentation reste en français, mais le README gagne un
  résumé en anglais, et issues et pull requests sont acceptées dans les deux langues. Le
  code est déjà en anglais.
- **Réactivité** : premier retour sous 48 h sur une issue ou une pull request extérieure.
  C'est le facteur qui fait revenir un contributeur.
- **Ne pas doubler un contributeur** : une issue `good first issue` ou `help wanted` est
  réservée aux contributions extérieures. Si quelqu'un s'y manifeste, on lui répond
  d'abord (accueil, précisions, invitation à ouvrir une pull request) ; l'équipe ne
  l'implémente elle-même qu'après un délai annoncé dans l'issue, ou s'il n'y a pas de
  suite. Leçon des issues #29 et #30 : deux propositions extérieures restées sans réponse,
  puis traitées en interne.
- **Reconnaissance** : contributeurs cités dans les notes de version et dans le README.

## Axe 3 — Se faire connaître

Ne communiquer **qu'après** l'axe 1 : un visiteur qui ne peut pas essayer ne revient pas.

| Canal | Public | Format |
|---|---|---|
| LinuxFr.org | Communauté libre francophone | Dépêche de lancement, puis une par version majeure |
| r/selfhosted, r/sysadmin, r/ITIL | Administrateurs, auto-hébergeurs | Présentation avec captures et mode démo |
| Show HN (Hacker News) | Développeurs anglophones | Lien vers la démo en ligne |
| awesome-selfhosted, AlternativeTo, Framalibre | Personnes qui cherchent un outil | Référencement (souvent conditionné à une version publiée) |
| dev.to, blog technique | Développeurs .NET / React | Article d'architecture : « un ITSM modulaire en .NET 8 et React » |
| Associations et communautés ITIL / ITSM francophones | Gestionnaires de processus | Présentation, retours d'usage |

**Image de partage** (1280×640) dans les paramètres du dépôt : c'est elle qui s'affiche
quand le lien circule.

## Axe 4 — Financer le projet

### Canaux

| Canal | Pour | Mise en place |
|---|---|---|
| **GitHub Sponsors** | Particuliers et entreprises, dons récurrents | Profil à activer par le mainteneur ; fichier `.github/FUNDING.yml` |
| **Open Collective** | Entreprises qui veulent une facture et une dépense transparente | Hébergeur fiscal à choisir (à vérifier : Open Source Collective, Open Collective Europe) |
| **Sponsoring de fonctionnalité** | Une DSI qui finance un enrichissement du plan (F1 mesure des SLA, F4 calendrier des changements…) | Devis par fonctionnalité, livrée sous AGPL pour tous |
| **Support et accompagnement** | Organisations qui déploient en production | Offre de service : installation, durcissement, formation ITIL |
| **Programmes publics** | Projets libres d'intérêt général | Pistes à vérifier : référencement au SILL (socle interministériel de logiciels libres) pour l'adoption par le secteur public français, appels à projets de type NGI / NLnet |

### Ce que reçoit un sponsor

| Niveau | Contrepartie |
|---|---|
| Soutien | Nom dans les notes de version |
| Bronze | Logo dans le README et le wiki |
| Argent | Logo + vote sur l'ordre de la feuille de route |
| Or | Logo en tête + point mensuel avec le mainteneur + priorité sur les correctifs |

Montants à fixer au lancement des Sponsors ; commencer bas pour avoir des premiers
soutiens, qui comptent plus que le montant.

### Ce qui rassure un sponsor

Un sponsor finance un projet qui **durera**. Les signaux qui comptent : des versions
régulières, une CI au vert, une politique de sécurité, une feuille de route publique, et
**plus d'un mainteneur** à terme. La gouvernance (qui décide, comment devenir mainteneur)
sera écrite dans un `GOVERNANCE.md` dès le deuxième contributeur régulier.

## Décisions

Tranchées le 2026-10-06 selon les recommandations ci-dessous, sauf D5 (en attente).

| # | Question | Options | Recommandation | Décision |
|---|---|---|---|---|
| D1 | Publier un **mode démonstration** avec annuaire Samba AD dans le dépôt ? | Oui, profil compose `demo` générique / Non, rester sur l'AD du client | **Oui** : c'est le frein n° 1 ; seule la version générique est publiée, sans rien de l'infrastructure locale | **Oui** — [ADR-0009](decisions/adr-0009-mode-demonstration-public.md) |
| D2 | **Accord de contribution** | DCO (signature `Signed-off-by`, léger) / CLA (cession de droits, permet une double licence commerciale future) | **DCO** si le modèle reste 100 % AGPL ; **CLA** seulement si une licence commerciale est envisagée — à trancher **avant** la première contribution extérieure, impossible à rattraper ensuite ([ADR-0006](decisions/adr-0006-licence-agplv3.md)) | **DCO** — [ADR-0010](decisions/adr-0010-certificat-d-origine-dco.md) |
| D3 | **Langue** | Français seul / README bilingue / tout bilingue | **README bilingue**, documentation en français : la cible est francophone, l'anglais ouvre la porte aux développeurs | **README bilingue** |
| D4 | **Canaux de financement** | Sponsors, Open Collective, support | **GitHub Sponsors** d'abord (immédiat), Open Collective quand une entreprise demande une facture | **Sponsors d'abord** — [ADR-0011](decisions/adr-0011-financement-sponsoring-et-services.md) |
| D5 | **Démo en ligne** | Oui / non, coût d'hébergement | Après D1, quand un premier sponsor couvre l'hébergement | en attente |

## Feuille de route sur 90 jours

| Période | Objectif | Indicateur |
|---|---|---|
| Semaines 1–2 | D1 à D4 tranchées ; code de conduite, `FUNDING.yml`, Discussions, résumé anglais | Profil communautaire GitHub à 100 % |
| Semaines 3–6 | Mode démonstration, `v0.1.0` publiée avec images `ghcr.io`, 10 issues prêtes à prendre | Essai réussi en 5 minutes sur une machine vierge |
| Semaines 7–8 | Lancement : LinuxFr, Reddit, référencements | 100 étoiles, 5 forks |
| Semaines 9–12 | Accueil des premières contributions, démo en ligne, premiers sponsors | 3 contributeurs extérieurs fusionnés, 3 sponsors |

## Indicateurs suivis

Relevés chaque mois dans le journal : étoiles, forks, contributeurs extérieurs (premières
contributions fusionnées), délai du premier retour sur issue et pull request, nombre de
sponsors et montant mensuel, téléchargements des images.
