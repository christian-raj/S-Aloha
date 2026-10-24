# ADR-0021 — Langage visuel v2 : sombre par défaut, nouvel emblème, polices servies par l'application

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-23
- **Statut** : accepté — supersède en partie [ADR-0005](adr-0005-langage-visuel-a-jetons-en-css-natif.md)
- **Décideur** : Leipa

## Contexte

Le langage visuel d'[ADR-0005](adr-0005-langage-visuel-a-jetons-en-css-natif.md) (Jost,
angles droits, thème clair par défaut, marque « S » sur un carré en dégradé) a donné une
identité à S-Aloha, mais il paraît daté à côté des plateformes que ses utilisateurs
fréquentent, en particulier les outils de sécurité comme OpenCTI, pris comme inspiration.
Trois limites concrètes s'y ajoutent :

- la police est chargée depuis Google Fonts : une installation sans accès Internet perd sa
  typographie, et la CSP doit autoriser deux domaines externes ;
- `--text-tertiary` et, en clair, les textes émeraude et ambre restaient sous 4,5:1 ;
- la marque, un « S » manuscrit sur un carré, se lit mal en petit et ne se décline pas.

## Décision

Garder l'architecture d'ADR-0005 (jetons en CSS natif dans `frontend/src/styles.css`,
classes `:root` / `.dark`, alias historiques, mêmes noms de classes) et en changer le
langage :

- **Sombre par défaut** : le script de thème d'`index.html` pose `.dark` sauf si
  l'utilisateur a choisi le mode clair (bascule mémorisée). Les deux thèmes restent complets.
- **Surfaces en couches** bleu nuit (`--bg-app` → `--bg-surface` → `--bg-surface-raised` →
  `--bg-surface-hover`) ; barre latérale sur `--bg-nav`, sombre dans les deux thèmes.
- **Couleurs** : un bleu pour l'action (`--accent-blue`), un turquoise « lagon » pour la
  marque (`--accent-teal`, `--brand-teal`), statuts inchangés dans leur sens ; tous les
  textes à 4,5:1 au moins sur toutes les surfaces, dans les deux thèmes ; texte sur aplat
  d'accent par `--on-accent` ; anneau de focus `--focus-ring`.
- **Angles adoucis** : `--radius-xs` 3 px à `--radius-lg` 14 px, à la place des angles droits.
- **Polices** : Geologica pour les titres et les chiffres, IBM Plex Sans pour le texte,
  IBM Plex Mono pour les références. Licence OFL, fichiers **servis par l'application**
  (`frontend/src/assets/fonts`), plus d'appel à Google Fonts ; la CSP se resserre
  (`style-src 'self'`, `font-src 'self'`).
- **Emblème** : un « S » hexagonal d'un seul trait dans un hexagone en filet, avec un
  losange au départ du trait — le « premier point » d'*Aloha*. Trait en dégradé bleu →
  turquoise sur fond sombre, encres foncées sur fond clair. Il remplace la marque dans la
  barre latérale, la barre mobile, la page de connexion et le favicon (`public/favicon.svg`,
  l'emblème sur une tuile marine arrondie). Le losange devient le repère des sections de
  navigation et des listes de la console.

Détail des jetons et des composants : [`frontend.md`](../reference/frontend.md#design).

## Conséquences

- Les captures d'écran de `docs/screenshots/` sont obsolètes et doivent être régénérées.
- Environ 480 Ko de polices sont livrés avec l'interface (mis en cache par le navigateur).
- Un utilisateur qui n'avait jamais touché la bascule passe en sombre, même si son système
  est en clair ; un clic sur la bascule le ramène en clair durablement.
- Le dégradé de la marque est réservé à l'emblème, au liseré du lien actif et au
  soulignement de l'onglet actif : il n'habille ni boutons ni fonds.

## Alternatives envisagées

- **Garder le clair par défaut, en suivant la préférence du système** — écarté : le
  sombre est l'identité visée ; la bascule reste disponible.
- **Continuer à charger les polices depuis Google Fonts** — écarté : contraire à
  l'installation sur site sans Internet et à une CSP sans domaine externe.
- **Reprendre l'identité d'OpenCTI** (couleurs exactes, emblème) — écarté : inspiration
  seulement ; S-Aloha garde sa propre marque.
