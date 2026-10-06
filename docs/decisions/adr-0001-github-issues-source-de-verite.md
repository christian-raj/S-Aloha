# ADR-0001 — GitHub Issues comme source de vérité pour les bugs, la sécurité et le backlog

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

Jusqu'ici, les points à durcir et les défauts connus vivaient dans le `README.md`
(section « Points de durcissement avant production ») et dans les messages de commit.
Ce mode a deux défauts :

- pas de statut interrogeable (ouvert/fermé, label) sans relire le fichier ;
- un tableau markdown dérive inévitablement par rapport à l'état réel du code.

## Décision

[GitHub Issues](https://github.com/christian-raj/S-Aloha/issues) est la source de vérité
pour les bugs, les items de sécurité et le backlog. Chaque item ouvert a une issue, avec
un label (`bug`, `security`, `enhancement`, `documentation`).

[`plan-action.md`](../plan-action.md) ne duplique pas les issues : il porte les
**chantiers** — ce qui demande une intention (un nouveau processus ITIL, le passage aux
migrations EF), et renvoie vers les issues qui les découpent.

## Conséquences

- Un défaut constaté → `gh issue create --label …` ; un défaut corrigé → l'issue est
  fermée avec la référence du commit.
- Les constats d'audit restent rédigés dans [`archives/`](../archives/) ; l'issue porte le
  lien vers ce détail plutôt que de le recopier.
- Risque accepté : l'historique des issues dépend de GitHub. Atténué par les constats
  datés conservés dans le dépôt.

## Alternatives envisagées

- **Suivi 100 % local** (liste dans un fichier markdown) — écarté : c'est l'état de départ,
  et il ne permet ni filtrage ni statut.
- **Outil de ticketing dédié** (Jira, Linear) — écarté : coût d'outillage injustifié pour
  un projet à un seul décideur ; GitHub Issues est déjà là où vit le code.
