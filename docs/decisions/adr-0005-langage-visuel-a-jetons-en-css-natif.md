# ADR-0005 — Nouveau langage visuel à jetons clair/sombre, réalisé en CSS natif

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

La première interface (fond neutre `#f7f8f9`, accents bleus, police Manrope, coins
arrondis) était fonctionnelle mais sans identité. Le passage à S-Aloha, plateforme
multi-processus ([ADR-0003](adr-0003-plateforme-modulaire-par-processus-itil.md)), demande
une navigation capable d'accueillir huit processus, une identité de marque et un mode
sombre.

## Décision

Adopter un langage visuel à **jetons**, réalisé en **CSS natif** dans
`frontend/src/styles.css`, sans framework CSS
([ADR-0004](adr-0004-conserver-le-stack-dotnet-react.md)) :

- **jetons** en variables CSS sur `:root` (clair, par défaut) et `.dark` (sombre) :
  fonds, textes, bordures, accents, ombres ;
- **angles droits** (aucun arrondi) ;
- police **Jost** ;
- **sidebar marine** en dégradé (272 px, toujours sombre quel que soit le thème), en
  sections *Pilotage* / *Processus ITIL* ;
- **canevas à points** en fond de contenu, avec lueurs de marque en coin ;
- **bascule clair / sombre**, mémorisée dans le navigateur.

Les anciennes variables (`--navy`, `--muted`, `--line`…) sont conservées comme **alias**
vers les jetons : les pages qui les utilisent en style en ligne suivent le thème sans
être réécrites.

Détail des jetons et des composants : [`frontend.md`](../reference/frontend.md#design).

## Conséquences

- Toute nouvelle couleur passe par un jeton défini dans les **deux** thèmes ; une couleur
  en dur dans une page ne suit pas le mode sombre.
- Les captures d'écran de la documentation antérieures au 2026-10-06 sont obsolètes et
  doivent être régénérées.
- La police est chargée depuis Google Fonts : un poste sans accès Internet retombe sur la
  police système (`system-ui`).

## Alternatives envisagées

- **Conserver le design précédent** — écarté : pas de structure de navigation pour
  plusieurs processus, pas de mode sombre, pas d'identité de marque.
- **Tailwind** — écarté par [ADR-0004](adr-0004-conserver-le-stack-dotnet-react.md).
- **Bibliothèque de composants** (MUI, Ant Design) — écarté : identité visuelle imposée,
  poids du bundle, et contraire au choix initial d'un frontend sans dépendance UI lourde.
