# Documentation S-Aloha

Cette base documentaire est rangée par **nature de document**, pas par thème. Un même
sujet — la sécurité, un processus ITIL — produit à la fois un constat daté, un plan et une
décision, et ces trois-là n'ont ni la même durée de vie ni le même lecteur. Les mélanger
dans un fichier unique est ce qui fait dériver une documentation.

## Comment c'est rangé

La méthode de travail se fait en trois étapes, et chacune a son emplacement :

| Étape | Ce qu'elle produit | Où ça vit | Durée de vie |
|---|---|---|---|
| **1. Analyse** — audit, diagnostic, revue | un constat **daté** | [`archives/`](archives/) | figé, on n'y corrige jamais rien |
| **2. Plan** — ce qu'il reste à faire | des **chantiers** | [`plan-action.md`](plan-action.md) | vivant, se vide en fermant |
| **3. Décision** — ce qui est tranché | un **ADR** | [`decisions/`](decisions/) | définitif, remplacé jamais réécrit |

Et au milieu, le socle : [`reference/`](reference/) — **ce qui est vrai en permanence**,
qui se met à jour et ne s'archive jamais.

Le cycle : un audit produit un constat daté qui part en `archives/`. Ce qu'il révèle de
durable est replié dans `reference/`. Ce qu'il reste à faire va dans `plan-action.md`. Ce
qui est tranché devient un ADR. Rien ne reste en suspens dans un document hybride.

## Le socle — [`reference/`](reference/)

| Document | Contenu |
|---|---|
| [`produit.md`](reference/produit.md) | Vision S-Aloha, les six piliers, cartographie des processus ITIL et leur état |
| [`regles-metier.md`](reference/regles-metier.md) | **Règles métier** — à lire avant toute modification de logique |
| [`architecture.md`](reference/architecture.md) | Containers, socle et modules, authentification AD → JWT, API |
| [`base-de-donnees.md`](reference/base-de-donnees.md) | Entités, contraintes, formats JSON des analyses, gestion du schéma |
| [`frontend.md`](reference/frontend.md) | Structure React, registre des modules, routes, thème et design |
| [`exploitation.md`](reference/exploitation.md) | Démarrage, configuration, Active Directory, déploiement |
| [`securite.md`](reference/securite.md) | Modèle d'autorisation, secrets, surface exposée, points de durcissement |
| [`glossaire.md`](reference/glossaire.md) | Termes ITIL, termes techniques, vocabulaire malgache |

## À la racine

| | |
|---|---|
| [`plan-action.md`](plan-action.md) | **Étape 2** — chantiers ouverts et priorités |
| [`screenshots/`](screenshots/) | Captures d'écran utilisées par le [`README`](../README.md) du dépôt |

## Où va quoi

- Un **bug** → [GitHub Issues](https://github.com/christian-raj/S-Aloha/issues), source de
  vérité ([ADR-0001](decisions/adr-0001-github-issues-source-de-verite.md)). Pas dans un
  tableau markdown, qui ne peut que dériver par rapport à l'issue.
- Un **chantier** (ce qui demande une intention, pas seulement un correctif) →
  [`plan-action.md`](plan-action.md).
- Un **rapport d'audit ou un constat** → [`archives/`](archives/), daté dans son nom.
- Une **décision structurante et actée** → un ADR. Critères et forme :
  [`decisions/readme.md`](decisions/readme.md).
- Un **fait durable sur le fonctionnement du système** → le document de
  [`reference/`](reference/) concerné, mis à jour sur place, **dans le même commit que le
  code** ([ADR-0002](decisions/adr-0002-documentation-dans-le-depot.md)).

## Ce qui vit hors de `docs/`

| Fichier | Pourquoi il ne bouge pas |
|---|---|
| `README.md` (racine) | Vitrine du dépôt, affichée par GitHub sur la page d'accueil |

## Conventions

- **Français** pour la documentation, **anglais** pour le code (identifiants), commentaires
  de code en français comme dans le reste du dépôt.
- **Noms de fichiers en minuscules**, mots séparés par des tirets.
- **Un document, une nature.** Si un fichier contient à la fois un constat, un plan et une
  description du fonctionnement, il faut le scinder.
- Les liens entre documents sont **relatifs**. Contrôle des liens, ancres et chemins cités :

  ```bash
  python3 scripts/check-docs.py
  ```

- Les commentaires de code peuvent citer la documentation (`docs/reference/frontend.md
  § Design`, `ADR-0005`) : renommer un document ou un titre impose de mettre à jour ces
  renvois — `grep -rn "docs/\|ADR-" backend frontend/src`.
- Aucune mention d'outil d'IA (signature, `Co-Authored-By`) dans les commits, les PR ou
  les fichiers.
