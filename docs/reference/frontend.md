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
    │       ├── Login.jsx         # panneau de marque, six piliers, formulaire AD
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
propre au processus, passé en enfant de `RecordDetail` : ouverture d'un problème depuis un
incident, relations entre CI, SLA d'un service. Les statuts réservés aux gestionnaires
sont désactivés dans la liste pour les autres rôles ; l'API reste seule juge.

Règle de dépendance : `modules/*` peut importer `core/*` ; `core/*` n'importe aucun module,
sauf le registre `modules/registry.js` (lu par `Layout` et `Login`).

## Routes

| Route | Page | Section de navigation |
|---|---|---|
| `/login` | `core/pages/Login` | — (publique) |
| `/` | `core/pages/Console` | Pilotage |
| `/reports` | `core/pages/Reporting` | Pilotage |
| `/problems` | `modules/problem-management/pages/Problems` | Processus ITIL › Problèmes |
| `/problems/:id` | `modules/problem-management/pages/ProblemDetail` | Processus ITIL › Problèmes |
| `/actions` | `modules/problem-management/pages/Actions` | Processus ITIL › Problèmes |
| `/incidents`, `/incidents/:id` | `modules/incident-management/pages/*` | Processus ITIL › Incidents |
| `/requests`, `/requests/:id` | `modules/service-request-management/pages/*` | Processus ITIL › Demandes |
| `/changes`, `/changes/schedule`, `/changes/:id` | `modules/change-enablement/pages/*` | Processus ITIL › Changements |
| `/configuration`, `/configuration/:id` | `modules/service-configuration-management/pages/*` | Processus ITIL › Configuration |
| `/services`, `/services/:id`, `/agreements`, `/agreements/:id` | `modules/service-level-management/pages/*` | Processus ITIL › Niveaux de service |
| `/knowledge`, `/knowledge/:id` | `modules/knowledge-management/pages/*` | Processus ITIL › Connaissances |
| `/improvements`, `/improvements/:id` | `modules/continual-improvement/pages/*` | Processus ITIL › Amélioration (CSI) |

Toutes les routes sauf `/login` passent par `Private` (jeton présent) et sont rendues dans
`Layout`. Les droits réels sont contrôlés par l'API.

## Registre des modules

`frontend/src/modules/registry.js` exporte :

- `PILLARS` — les six piliers S-A-L-O-H-A (`id`, `letter`, `label`, `desc`). Deux piliers
  portent la lettre « A » : la clé est l'`id`, jamais la lettre ;
- `MODULES` — les processus ITIL, dans l'ordre du cycle de vie : `id`, `label`,
  `description`, `pillar`, `status` (`active` | `soon`) et, pour un module actif, `href`
  (point d'entrée), `routes` (préfixes d'URL qui l'activent dans la navigation), `pages`
  (sous-entrées affichées quand le module est ouvert, facultatives ; quand une sous-page en
  prolonge une autre — `/changes/schedule` sous `/changes` — la correspondance exacte l'emporte) ;
- `pillarOf(id)`.

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

### Principes

- **Angles droits** : aucun `border-radius`.
- **Police Jost** (Google Fonts), repli `system-ui`.
- **Sidebar marine** de 272 px en dégradé, toujours sombre, en deux sections : *Pilotage*
  (Console, Reporting) et *Processus ITIL* (le registre).
- **Canevas à points** (`.bg-canvas`) : grille de points fine et lueurs de marque en coin,
  pour éviter un fond plat sur les pages peu denses.
- **Badges** de statut, de priorité et de rôle RACI ; tableaux cliquables.
- **Accessibilité** : `:focus-visible` marqué, `aria-label` sur les boutons-icônes.
- **Jamais de composant déclaré dans le rendu d'un autre** : React le recrée à chaque
  rendu et la saisie perd le focus (défaut M1 de l'arbre des défaillances). Un sous-composant
  se déclare au niveau du module (ex. `FtaNode` dans `FtaTree.jsx`).

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
