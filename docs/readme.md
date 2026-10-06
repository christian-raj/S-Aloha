# 📚 Documentation S-Aloha

> Tout ce qu'il faut pour comprendre, installer et faire évoluer S-Aloha.
> Première visite ? Commencez par le [README](../README.md), puis le [Produit](reference/produit.md).

## Référence

Ce qui est vrai en permanence, mis à jour avec le code.

| | Document | Pour |
|---|---|---|
| 🧭 | [Produit](reference/produit.md) | Comprendre la vision, les processus couverts et la feuille de route |
| 📐 | [Règles métier](reference/regles-metier.md) | Connaître les rôles, la console, le cycle de vie, la priorité, le RACI |
| 🏗️ | [Architecture](reference/architecture.md) | Voir comment tout s'assemble : containers, socle, modules, API |
| 🗄️ | [Base de données](reference/base-de-donnees.md) | Lire le modèle de données et les formats d'analyse |
| 🖥️ | [Frontend](reference/frontend.md) | Ajouter un écran ou un module, respecter le design |
| ⚙️ | [Exploitation](reference/exploitation.md) | Installer, brancher l'Active Directory, mettre à jour |
| 🛡️ | [Sécurité](reference/securite.md) | Préparer une mise en production |
| 📖 | [Glossaire](reference/glossaire.md) | Retrouver un terme ITIL, technique ou malgache |

## Décisions, plan et historique

| | | Durée de vie |
|---|---|---|
| ⚖️ [Décisions (ADR)](decisions/readme.md) | Les choix structurants et **pourquoi** | Définitif : remplacé, jamais réécrit |
| 🗺️ [Plan d'action](plan-action.md) | Les chantiers ouverts | Vivant : se vide en avançant |
| 🗃️ [Archives](archives/readme.md) | Constats datés et journaux des sujets traités | Figé |
| 🔄 [Protocole](protocole-documentation.md) | La routine qui tient tout cela à jour | Vivant |
| 🌱 [Stratégie communauté](strategie-communaute.md) | Attirer contributeurs et sponsors | Revue chaque trimestre |

Le cycle : un constat daté part en **archives** ; ce qu'il révèle de durable rejoint la
**référence** ; ce qu'il reste à faire va au **plan** ; ce qui est tranché devient une
**décision**.

## Où va quoi

| J'ai… | Je le mets dans… |
|---|---|
| un bug, une faille | [GitHub Issues](https://github.com/christian-raj/S-Aloha/issues) ([ADR-0001](decisions/adr-0001-github-issues-source-de-verite.md)) |
| un chantier qui demande une intention | [`plan-action.md`](plan-action.md) |
| un audit, un état des lieux | `archives/`, daté dans le nom du fichier |
| un sujet traité aujourd'hui | le journal du jour, `archives/journal-AAAA-MM-JJ.md` ([protocole](protocole-documentation.md)) |
| une décision actée et structurante | un ADR dans [`decisions/`](decisions/readme.md) |
| un fait durable sur le fonctionnement | le document de référence concerné, **dans le même commit que le code** ([ADR-0002](decisions/adr-0002-documentation-dans-le-depot.md)) |

## Wiki

Le [wiki du projet](https://github.com/christian-raj/S-Aloha/wiki) est généré depuis ce
dossier par `scripts/sync-wiki.py` : on le lit, on ne l'édite pas
([protocole § 6](protocole-documentation.md#6-publier-le-wiki)).

## Licence

Code et documentation sont sous [AGPLv3](../LICENSE)
([ADR-0006](decisions/adr-0006-licence-agplv3.md)).

## Conventions

- Documentation en **français**, identifiants de code en **anglais**.
- Fichiers en **minuscules-avec-tirets** ; liens **relatifs**.
- **Un document, une nature** : constat, plan, décision ou référence, jamais un mélange.
- Diagrammes en **Mermaid**, affichés directement par GitHub.
- Captures d'écran dans [`screenshots/`](screenshots/), à régénérer quand l'interface change.
- Le code cite parfois la doc (`docs/reference/frontend.md § Design`, `ADR-0005`) : un
  renommage impose de mettre à jour ces renvois (`grep -rn "docs/\|ADR-" backend frontend/src`).
- Avant de committer :

  ```bash
  python3 scripts/check-docs.py   # liens, ancres et chemins cités
  ```
