# 🖥️ Frontend

> React 18 + Vite + react-router, servi par nginx. Aucune bibliothèque de composants : CSS natif à jetons ([ADR-0004](../decisions/adr-0004-conserver-le-stack-dotnet-react.md), [ADR-0005](../decisions/adr-0005-langage-visuel-a-jetons-en-css-natif.md)).

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## Structure

```
frontend/
├── index.html                    # métadonnées, police Jost, script de thème avant rendu
├── nginx.conf                    # sert le build, proxifie /api/ vers le service api
├── public/favicon.svg
└── src/
    ├── main.jsx
    ├── api.js                    # client fetch + JWT (sessionStorage) ; 401 ⇒ retour /login
    ├── App.jsx                   # déclaration des routes
    ├── styles.css                # jetons, thèmes, composants — voir § Design
    ├── core/                     # socle UI, transverse aux processus
    │   ├── about.js              # URL du code source et licence (AGPLv3, VITE_SOURCE_URL)
    │   ├── fields.js             # champs communs (titre, description, responsable AD), formats de date
    │   ├── itemTypes.js          # types reliables → libellé et page (miroir de ItemLinks côté API)
    │   ├── components/
    │   │   ├── Layout.jsx        # sidebar (desktop) / tiroir (mobile), construite depuis le registre
    │   │   ├── ThemeToggle.jsx   # bascule clair / sombre
    │   │   ├── icons.jsx         # icônes SVG en ligne
    │   │   ├── DirectoryPicker.jsx # recherche dans l'annuaire AD (utilisateur ou groupe)
    │   │   ├── RaciEditor.jsx    # éditeur RACI (rôle + DirectoryPicker)
    │   │   ├── RecordList.jsx    # registre d'un processus : filtres, création, tableau
    │   │   ├── RecordDetail.jsx  # fiche : en-tête, éditeur, contenu propre, éléments liés
    │   │   ├── RecordForm.jsx    # formulaire décrit par des champs ; conversions API ↔ formulaire
    │   │   ├── StatusBadge.jsx   # badge de statut selon la tonalité déclarée
    │   │   └── LinkedItems.jsx   # éléments liés (tous processus), ajout par référence
    │   └── pages/
    │       ├── Login.jsx         # panneau de marque (fonctionnalités : FEATURES), formulaire AD
    │       ├── Console.jsx       # « Ma console », orientée action
    │       └── Reporting.jsx     # indicateurs et volumétrie
    └── modules/
        ├── registry.js           # registre des processus ITIL — source unique de la navigation
        ├── problem-management/
        │   ├── components/       # FiveWhys, Ishikawa, FtaTree
        │   └── pages/            # Problems, ProblemDetail (onglets), Actions
        ├── incident-management/          # config.jsx + pages Incidents, IncidentDetail
        ├── service-request-management/   # config.jsx + pages Requests, RequestDetail
        ├── change-enablement/            # config.jsx + pages Changes, ChangeDetail, ChangeSchedule
        ├── service-configuration-management/ # config.jsx + pages ConfigurationItems, ConfigurationItemDetail
        ├── service-level-management/     # config.jsx + pages Services, ServiceDetail, Agreements, AgreementDetail
        ├── knowledge-management/         # config.jsx + pages Articles, ArticleDetail
        └── continual-improvement/        # config.jsx + pages Improvements, ImprovementDetail
```

### Modules décrits par configuration

Hors problèmes, chaque module tient dans un `config.jsx` lu par les composants génériques
du socle (`RecordList`, `RecordDetail`) : client d'API (`api.js` › `records(path)`),
`basePath`, `linkType`, statuts avec tonalité (`tone` : `new`, `progress`, `wait`, `done`,
`bad`, `closed` — classes `.badge.t-*`) et marque `manager` des statuts réservés, champs du
formulaire (`text`, `textarea`, `select`, `date`, `datetime`, `number`, `checkbox`, `person`),
colonnes du registre, badges et mentions de l'en-tête. Une page n'ajoute que ce qui est
propre au processus :

- `tab = { label, render(record, reload) }` : l'**onglet propre à la pratique** — Conflits
  (changement), Relations et impact (CI), Accords (SLA) (service) ; une pratique à
  plusieurs onglets passe une **liste** `[{ label, render }]` (évaluation NIS 2 :
  Questionnaire, Synthèse) ;
- `children(record, reload)` : complément de l'onglet **Informations** (ex. « Ouvrir un
  problème lié » et l'encadré « Cas similaires » sur un incident ; « Cas similaires » sur un
  problème) ;
- `RecordList` accepte un `intro` affiché sous le titre du registre (bloc « Chercher
  d'abord » des connaissances).

`RecordDetail` affiche les onglets dans l'ordre **Informations → onglet de la pratique →
Liens** (Liens si `config.linkType`). Un onglet vide affiche un message explicite ; une
fiche s'ouvre toujours sur Informations. La fiche Problème, hors socle, suit le même ordre :
Informations · Analyse de cause racine · Actions correctives · Liens. Les statuts réservés aux gestionnaires
sont désactivés dans la liste pour les autres rôles ; l'API reste seule juge.

Règle de dépendance : `modules/*` peut importer `core/*` ; `core/*` n'importe aucun module,
sauf le registre `modules/registry.js` (lu par `Layout`).

## Routes

| Route | Page | Section de navigation |
|---|---|---|
| `/login` | `core/pages/Login` | — (publique) |
| `/` | `core/pages/Console` | Pilotage |
| `/reports` | `core/pages/Reporting` | Pilotage |
| `/problems` | `modules/problem-management/pages/Problems` | Pratiques › Problèmes |
| `/problems/:id` | `modules/problem-management/pages/ProblemDetail` | Pratiques › Problèmes |
| `/actions` | `modules/problem-management/pages/Actions` | Pratiques › Problèmes |
| `/incidents`, `/incidents/:id` | `modules/incident-management/pages/*` | Pratiques › Incidents |
| `/requests`, `/requests/:id` | `modules/service-request-management/pages/*` | Pratiques › Demandes |
| `/changes`, `/changes/schedule`, `/changes/:id` | `modules/change-enablement/pages/*` | Pratiques › Changements |
| `/configuration`, `/configuration/:id` | `modules/service-configuration-management/pages/*` | Pratiques › Configuration |
| `/services`, `/services/:id`, `/agreements`, `/agreements/:id` | `modules/service-level-management/pages/*` | Pratiques › Niveaux de service |
| `/knowledge`, `/knowledge/:id` | `modules/knowledge-management/pages/*` | Pratiques › Connaissances |
| `/improvements`, `/improvements/:id` | `modules/continual-improvement/pages/*` | Pratiques › Amélioration (CSI) |
| `/assessments`, `/assessments/:id` | `modules/compliance-assessment/pages/Assessments`, `AssessmentDetail` | Pratiques › Conformité NIS 2 |
| `/settings/nis2` | `modules/compliance-assessment/pages/ReferentialSettings` | Paramétrage › Référentiel NIS 2 (Admin) |

Toutes les routes sauf `/login` passent par `Private` (jeton présent) et sont rendues dans
`Layout`. Les droits réels sont contrôlés par l'API.

## Registre des modules

`frontend/src/modules/registry.js` exporte :

- `MODULES` — les processus ITIL, dans l'ordre du cycle de vie : `id`, `label`,
  `description`, `status` (`active` | `soon`) et, pour un module actif, `href`
  (point d'entrée), `routes` (préfixes d'URL qui l'activent dans la navigation), `pages`
  (sous-entrées affichées quand le module est ouvert, facultatives ; quand une sous-page en
  prolonge une autre — `/changes/schedule` sous `/changes` — la correspondance exacte l'emporte) ;
- `SECTIONS` — les sections de menu après Pilotage et Pratiques : **Paramétrage**
  (`id: 'settings'`, `roles: ['Manager','Admin']`) et **Administration**
  (`id: 'administration'`, `roles: ['Admin']`), chacune avec ses `entries`. Une entrée :
  `{ href, label, description, icon, roles? }`, où `icon` est une clé de `SECTION_ICONS`
  (`'settings'` ou `'admin'`). Une section n'est affichée que si au moins une de ses
  entrées est visible pour le rôle : aujourd'hui, aucune ne l'est (pas de section vide,
  vérifié par `Layout.sections.test.jsx` sur le registre réel). Le titre de la section
  Administration porte un marqueur ambre (`.nav-section.admin`) ;
- `roles` (section, module ou sous-entrée, facultatif) : rôles qui voient l'entrée ; absent, tous.
  `visibleFor(entry, role)` décide ; la barre latérale masque les entrées réservées (l'API
  reste seule juge des droits) ;

**Sous-entrée filtrée** : un `href` peut porter un filtre de registre
(`/problems?status=Erreur+connue`) ; la page lit ce filtre dans l'URL et adapte son titre.
Le filtre s'encode avec `URLSearchParams` **des deux côtés** (registre et page) : l'espace
devient « + », sinon les URL ne se comparent pas.

**Sous-entrée active** : correspondance de l'URL complète (chemin et filtre), puis du
chemin, puis du préfixe. Les sous-entrées sont des `Link` (et non des `NavLink`, qui ne
compare que le chemin) ; l'entrée active porte `aria-current="page"`.

Un module `soon` apparaît grisé, non cliquable, avec le badge « Bientôt ».

### Ajouter un module

1. Créer `src/modules/<processus>/` : un `config.jsx` (voir § Modules décrits par
   configuration) et des pages qui passent cette configuration à `RecordList` et
   `RecordDetail` ; ajouter le client dans `api.js` et le type dans `core/itemTypes.js`.
2. Déclarer ses routes dans `App.jsx`, sous le commentaire du processus.
3. Dans `registry.js`, passer le module à `status: 'active'` et renseigner `href`, `routes`,
   `pages`. Rien d'autre à toucher dans la navigation.
4. Côté backend : [`architecture.md`](architecture.md#3-organisation-du-code--socle-et-modules).
5. Mettre à jour [`produit.md`](produit.md#cartographie-des-processus-itil-et-état).

## Navigation cible

> Consigne pour la conception de l'interface (agent Design compris) : la navigation
> **dérive du [référentiel fonctionnel](processus/readme.md)**. Chaque entrée ci-dessous
> renvoie à la règle qui la justifie ; une entrée 🔜 apparaît dans le menu **quand sa
> règle est implémentée**, jamais avant (pas de page vide). Le registre
> `frontend/src/modules/registry.js` reste la source unique du menu.

### Principes

- **Ordre du cycle de vie du service** pour les pratiques (incidents → demandes →
  problèmes → changements → configuration → niveaux de service → connaissances →
  amélioration), comme aujourd'hui.
- Chaque pratique ouvre sur son **registre** ; les sous-entrées sont les autres **objets**
  de la pratique (§ 3 de son document) et ses **vues** (calendrier, impact).
- **Quatre sections**, dans cet ordre : **Pilotage** (tous), **Pratiques** (tous),
  **Paramétrage** (Manager, Admin) et **Administration** (Admin seul).
- **Paramétrage** = le **fonctionnement des pratiques** : catalogues, modèles, périodes,
  référentiels qui règlent la façon dont une pratique s'exerce. Délégable aux
  gestionnaires. **Administration** = la **plateforme** elle-même : accès et rôles,
  sécurité, santé, traçabilité. Admin seul.
- Une **file de travail** (brouillons à publier, CI à vérifier) n'est pas du paramétrage :
  elle reste une sous-entrée de sa pratique, éventuellement réservée par `roles`.
- Une section n'apparaît que si elle a **au moins une entrée visible** pour le rôle : pas de
  section vide (registre `SECTIONS`, champ `roles` des sections et des entrées).
- Ce qui est **propre à un enregistrement** (historique, commentaires, liens, impact) est un
  **onglet de sa fiche**, pas une entrée de menu.
- **Pilotage** reste transverse à toutes les pratiques.

### Pilotage

| Entrée | Visible par | Règle | Statut |
|---|---|---|---|
| Ma console | Tous | [socle § 2](regles-metier.md#2-consoles-par-rôle) | ✅ |
| Reporting (période choisie, export) | Tous | [SOC-18, SOC-19](regles-metier.md#7-reporting) | ✅ (page) / 🔜 |
| Notifications : **cloche** dans l'en-tête avec compteur, pas une entrée de menu | Tous | [SOC-21](regles-metier.md#8-notifications) | 🔜 |

### Pratiques

| Pratique | Sous-entrée | Visible par | Règle | Statut |
|---|---|---|---|---|
| [Incidents](processus/gestion-des-incidents.md) | Registre des incidents | Tous | INC-01 | ✅ |
| | Filtres prédéfinis du registre : mes incidents, non pris en charge, SLA en risque, majeurs | Tous | INC-06, INC-24, INC-12 | ✅ (filtres de base) / 🔜 |
| [Demandes](processus/gestion-des-demandes.md) | Registre des demandes | Tous | REQ-01 | ✅ |
| | Nouvelle demande **depuis le catalogue** (choix d'un modèle) | Tous | REQ-20 | 🔜 |
| [Problèmes](processus/gestion-des-problemes.md) | Registre des problèmes | Tous | PRB-01 | ✅ |
| | Actions correctives (suivi transverse) | Tous | PRB-15 | ✅ |
| | Erreurs connues : sous-entrée `/problems?status=Erreur+connue` | Tous | PRB-11 | ✅ |
| [Changements](processus/habilitation-des-changements.md) | Registre des changements | Tous | CHG-01 | ✅ |
| | Calendrier des changements (conflits, **périodes de gel**) | Tous | CHG-08, CHG-09, CHG-25 | ✅ / 🔜 (gel) |
| [Configuration](processus/gestion-de-la-configuration.md) | Registre des CI (filtre par type) | Tous | CFG-01, CFG-07 | ✅ |
| | CI à vérifier | Tous (propriétaires), Manager | CFG-15 | 🔜 |
| [Niveaux de service](processus/gestion-des-niveaux-de-service.md) | Catalogue des services | Tous | SLM-01 | ✅ |
| | Accords (SLA) | Tous | SLM-02 | ✅ |
| | Respect des SLA (tableau mensuel) | Tous | SLM-20, SLM-21 | 🔜 |
| [Connaissances](processus/gestion-des-connaissances.md) | Base de connaissances, avec le bloc « Chercher d'abord » (recherche hybride) en tête du registre | Tous | KB-04, RAG-05 | ✅ |
| | Brouillons à publier | Manager, Admin | KB-21 | 🔜 |
| [Amélioration](processus/amelioration-continue.md) | Registre d'amélioration | Tous | CSI-01 | ✅ |
| [Conformité NIS 2](processus/conformite-nis2.md) | Registre des évaluations (« Évaluations ReCyF (ANSSI) ») ; fiche : Informations · Questionnaire · Synthèse · Liens | Tous | NIS-02, NIS-04, NIS-10 | ✅ |
| | Déclarations réglementaires : registre, échéances ; encadré « Déclarations réglementaires » et qualification sur la fiche d'un incident cyber | Tous (décisions : Manager) | [DRG-01, DRG-10](processus/declarations-reglementaires.md), INC-41 | 🔜 |

### Paramétrage

Section réservée aux **Manager et Admin** ; elle apparaît avec sa première entrée
implémentée. Une entrée par objet de paramétrage, regroupées par pratique dans l'ordre du
cycle de vie.

| Entrée | Visible par | Règle | Statut |
|---|---|---|---|
| Catégories (incidents, problèmes) | Manager, Admin | [SOC-27](regles-metier.md#9-traçabilité-commentaires-administration) | 🔜 |
| Catalogue de demandes (modèles) | Manager, Admin | [REQ-20](processus/gestion-des-demandes.md) | 🔜 |
| Modèles de changement standard | Manager, Admin | [CHG-26](processus/habilitation-des-changements.md) | 🔜 |
| Périodes de gel | Manager, Admin | [CHG-25](processus/habilitation-des-changements.md) | 🔜 |
| Import des CI (CSV) | Admin | [CFG-30](processus/gestion-de-la-configuration.md) | 🔜 |
| Heures de service (calendriers) | Manager, Admin | [SLM-14](processus/gestion-des-niveaux-de-service.md) | 🔜 |
| Référentiel NIS 2 : structure embarquée et import du texte des exigences (`/settings/nis2`) | Admin | [NIS-20, NIS-21](processus/conformite-nis2.md) | ✅ |

Le **catalogue des services** reste dans la pratique Niveaux de service : c'est un objet de
la pratique, consulté par tous, pas un réglage.

« Référentiel NIS 2 » est la **première entrée réelle** de la section : réservée à l'Admin,
la section Paramétrage n'apparaît aujourd'hui que pour lui ; elle apparaîtra pour les
gestionnaires avec leur première entrée.

### Administration

Section réservée à l'**Admin** ; elle apparaît avec sa première entrée implémentée.

| Entrée | Contenu | Règle | Statut |
|---|---|---|---|
| Utilisateurs et rôles | Utilisateurs constatés, rôle effectif, mapping groupes AD → rôles (lecture) | [SOC-26](regles-metier.md#9-traçabilité-commentaires-administration) | 🔜 |
| Santé | Base, annuaire, version déployée | SOC-26 | 🔜 |
| Journal d'audit | Journal global, filtrable par auteur, pratique, période | [SOC-20](regles-metier.md#9-traçabilité-commentaires-administration), SOC-26 | 🔜 |
| Nettoyage | Doublons à fusionner ou supprimer | SOC-26 | 🔜 |
| Index de recherche | État du service d'embeddings et de l'index par source, reconstruction ; avis ambre si le service est injoignable | [RAG-08](recherche.md) | ✅ |

« Index de recherche » est la **première entrée réelle** de la section : un Admin voit
désormais les quatre sections (Pilotage, Pratiques, Paramétrage, Administration), un
Manager deux (Pilotage, Pratiques), tant qu'aucune entrée de Paramétrage ne lui est ouverte.

### Onglets communs des fiches

Toutes les fiches d'enregistrement partagent la même structure d'onglets, dans cet ordre ;
un onglet sans contenu reste visible avec un état vide explicite.

| Onglet | Contenu | Règle | Statut |
|---|---|---|---|
| Informations | Champs du § 3, actions de transition (§ 4) en boutons nommés par l'étape (« Prendre en charge », « Autoriser »…) | § 3 et § 4 de chaque pratique | ✅ (champs) / 🔜 (boutons de transition) |
| Propre à la pratique | Analyse de cause racine et Actions correctives (problème), Relations et impact (CI), Accords (SLA) (service), Conflits (changement) | PRB-05, CFG-04, SLM-02, CHG-09 | ✅ |
| Liens | Enregistrements reliés, toutes pratiques ; onglet dédié | SOC-12 | ✅ |
| Commentaires | Fil public / notes de travail | SOC-24 | 🔜 |
| Pièces jointes | | SOC-25 | 🔜 |
| Historique | Journal d'audit de l'enregistrement | SOC-20 | 🔜 |

Un bouton de transition n'apparaît que si la transition est **permise** depuis le statut
courant (tableau § 4) et **autorisée** pour le rôle (§ 2) ; ses conditions (motif, champ
obligatoire) s'affichent dans une boîte de dialogue avant l'envoi.

### Console

Les blocs à venir de chaque pratique sont listés au § 8 de son document, avec leur groupe
(Urgent, Décisions, Relances, Mon travail). La console garde ses groupes et son ordre
([socle § 2](regles-metier.md#2-consoles-par-rôle)) ; un nouveau bloc rejoint son groupe
sans en créer de nouveau.

## Design

Langage visuel à jetons, en CSS natif sans Tailwind
([ADR-0005](../decisions/adr-0005-langage-visuel-a-jetons-en-css-natif.md)). Tout est dans
`frontend/src/styles.css`.

### Jetons

Définis sur `:root` (thème clair, défaut) et redéfinis sur `.dark`. **Toute couleur passe
par un jeton** défini dans les deux thèmes.

| Famille | Jetons |
|---|---|
| Fonds | `--bg-app`, `--bg-surface`, `--bg-surface-raised`, `--bg-surface-hover`, `--bg-overlay` |
| Textes | `--text-primary`, `--text-secondary`, `--text-tertiary`, `--text-disabled` |
| Bordures | `--border-subtle`, `--border-default`, `--border-strong` |
| Accents | `--accent-{blue,emerald,red,amber,violet}` et leur variante `-soft` |
| Ombres | `--shadow-sm`, `--shadow-md`, `--shadow-lg` |
| Marque (indépendante du thème) | `--brand-navy`, `--brand-navy-deep`, `--brand-blue`, `--brand-gradient`, `--brand-tile` |
| Typographie | `--font-sans` (Jost), `--font-mono` |

**Alias historiques** — `--navy`, `--ink`, `--muted`, `--line`, `--blue`, `--blue-soft`,
`--ok`, `--warn`, `--danger` pointent vers les jetons. Ils existent pour les styles en
ligne des pages antérieures ; ne pas les utiliser dans du code nouveau.

### Typographie

- Échelle en jetons sur `:root` : `--fs-2xs` 11,5 · `--fs-xs` 12,5 · `--fs-sm` 14 ·
  `--fs-base` 15,5 · `--fs-md` 17 · `--fs-lg` 21 · `--fs-xl` 28 · `--fs-2xl` 36 px ;
  `html` à 16 px. **Toute nouvelle taille de police passe par ces jetons**, sans valeur en
  px en dur.
- Tailles courantes : cellules 15 px, champs 15,5, boutons 15, libellés de champ 14,
  badges 11,5, en-têtes de colonnes 12, titre de page 36 (30 en mobile), sous-titre 16,5
  en `--text-secondary`.

### Principes

- **Angles droits** : aucun `border-radius`.
- **Police Jost** (Google Fonts), repli `system-ui`.
- **Sidebar marine** de 272 px en dégradé, toujours sombre, en sections : *Pilotage*
  (Console, Reporting), *Pratiques* (le registre), puis *Paramétrage* et *Administration*
  selon le rôle (§ Navigation cible).
- **Canevas à points** (`.bg-canvas`) : grille de points fine et lueurs de marque en coin,
  pour éviter un fond plat sur les pages peu denses.
- **Badges** de statut, de priorité et de rôle RACI ; tableaux cliquables.
- **Accessibilité** : `:focus-visible` marqué, `aria-label` sur les boutons-icônes.
- **Jamais de composant déclaré dans le rendu d'un autre** : React le recrée à chaque
  rendu et la saisie perd le focus (défaut M1 de l'arbre des défaillances). Un sous-composant
  se déclare au niveau du module (ex. `FtaNode` dans `FtaTree.jsx`).

### Console

`core/pages/Console.jsx`, alimentée par `GET /api/console` (contenu par rôle :
[règles métier § 2](regles-metier.md#2-consoles-par-rôle)).

- En-tête : date et rôle, « Bonjour, <prénom> », phrase propre au rôle.
- Tuiles de synthèse (`.c-tile`) : Urgent, Décisions, Relances, Mon travail ; une tuile mène
  à son groupe, une tuile à zéro s'efface.
- Groupes dans l'ordre de traitement : Urgent, Décisions, Relances, Mon travail, À suivre.
- Listes compactes `.c-list` (référence, titre, métadonnées, chevron) plutôt que des
  tableaux ; ton de la liste par classe `tone-red`, `tone-blue`, `tone-amber`,
  `tone-emerald`, `tone-neutral`. Pas de bandeau ni d'émojis ; état vide « Tout est à jour ».

### Page de connexion

- Le panneau de marque (gauche) ne liste pas les modules. De
  haut en bas : marque, « Plateforme ITIL de la DSI », titre « Piloter le service IT, de
  l'incident à l'amélioration. », maxime malgache et sa
  traduction, explication du nom, puis « Ce que vous y faites » : quatre blocs de
  fonctionnalités (constante `FEATURES` de `Login.jsx` : console par rôle, cause racine et
  RACI AD, changements, mesure du service rendu).
- Message et blocs forment un seul ensemble centré verticalement : aucun défilement de
  1024×768 à 1920×1080 ; contraste ≥ 8:1 pour tous les textes du panneau.
- **Un bloc de fonctionnalité correspond à une fonctionnalité réelle** : le mettre à jour
  quand une fonctionnalité change (ex. « Cibles SLA » tant que l'atteinte des SLA n'est pas
  mesurée, chantier F1).

### Thème clair / sombre

- Le thème est une classe `dark` sur `<html>`.
- Il est posé **avant le premier rendu** par un script en tête d'`index.html` : préférence
  enregistrée (`localStorage`, clé `s-aloha-theme`), sinon préférence système
  (`prefers-color-scheme`). Décidé dans React, il ferait flasher l'écran en clair.
- `ThemeToggle` ne fait que basculer la classe et enregistrer le choix ; si le stockage est
  indisponible (navigation privée), le thème reste en mémoire pour la session.

### Responsive

| Largeur | Comportement |
|---|---|
| ≥ 1024 px | sidebar fixe ; page de connexion avec panneau de marque |
| < 1024 px | sidebar remplacée par une barre supérieure et un **tiroir** ; connexion compacte |
| ≤ 640 px | grilles sur une colonne, tableaux défilants horizontalement |

## Session côté navigateur

- Jeton JWT et profil (`token`, `user`) en `sessionStorage` : la session ne survit pas à la
  fermeture de l'onglet. Déconnexion = `sessionStorage.clear()`.
- Une réponse **401** de l'API vide la session et renvoie vers `/login`.
- Implications de sécurité : [`securite.md`](securite.md).
