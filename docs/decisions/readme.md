# ⚖️ Décisions d'architecture (ADR)

<sub>[← Documentation](../readme.md)</sub>

Une décision structurante par fichier, numérotée sans trou, jamais réécrite après coup :
si une décision est renversée, on en écrit une nouvelle qui la supersède et on met à jour
le statut de l'ancienne. C'est ce qui permet de retrouver *pourquoi* le projet est dans
son état actuel, et pas seulement *quel* est cet état.

| # | Décision | Date | Statut |
|---|---|---|---|
| [0001](adr-0001-github-issues-source-de-verite.md) | GitHub Issues comme source de vérité pour les bugs, la sécurité et le backlog | 2026-10-06 | accepté |
| [0002](adr-0002-documentation-dans-le-depot.md) | La documentation vit dans le dépôt ; un correctif et sa documentation tiennent dans un seul commit | 2026-10-06 | accepté |
| [0003](adr-0003-plateforme-modulaire-par-processus-itil.md) | Plateforme modulaire par processus ITIL : socle + modules + registre | 2026-10-06 | accepté |
| [0004](adr-0004-conserver-le-stack-dotnet-react.md) | On conserve le stack .NET / React / PostgreSQL ; pas de réécriture Next.js / Tailwind | 2026-10-06 | accepté |
| [0005](adr-0005-langage-visuel-a-jetons-en-css-natif.md) | Nouveau langage visuel à jetons clair/sombre, réalisé en CSS natif | 2026-10-06 | accepté |
| [0006](adr-0006-licence-agplv3.md) | S-Aloha est publié sous licence AGPLv3 | 2026-10-06 | accepté |
| [0007](adr-0007-pratiques-itil4-et-socle-commun-des-processus.md) | Pratiques ITIL 4 comme référentiel, socle commun des processus, liens inter-processus dans le socle | 2026-10-06 | accepté |

Les choix fondateurs antérieurs à cette base (authentification AD LDAP → JWT, analyses
RCA stockées en JSON) sont décrits dans [`reference/architecture.md`](../reference/architecture.md)
et non dans des ADR : ils n'ont pas été arbitrés entre alternatives au moment où ils ont
été faits. S'ils venaient à être remis en cause, la décision qui les remplace fera l'objet
d'un ADR.

## Quand écrire un ADR

Quand la décision est **structurante et durable** : elle change l'architecture, une
convention, un fournisseur, ou la façon de livrer — et quelqu'un se demandera dans six
mois pourquoi c'est ainsi.

Ne pas en écrire pour un correctif, un choix d'implémentation local, ou une décision
qu'un agent a prise seul sans validation. Un ADR consigne une décision **actée**, pas une
proposition.

## Forme

Nom de fichier : `adr-000X-titre-en-tirets.md`.

```markdown
# ADR-000X — Titre à l'affirmative

- **Date** : YYYY-MM-DD
- **Statut** : accepté | remplacé par ADR-000Y | abandonné
- **Décideur** : …

## Contexte      ce qui n'allait pas, avec les faits mesurés
## Décision      ce qu'on fait, à l'affirmative
## Conséquences  ce que ça implique, y compris les inconvénients acceptés
## Alternatives envisagées   et pourquoi elles ont été écartées
```

Le point qui a le plus de valeur six mois plus tard est **« Alternatives envisagées »** :
sans lui, on repropose périodiquement ce qui a déjà été écarté.
