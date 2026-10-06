# ADR-0004 — On conserve le stack .NET / React / PostgreSQL

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

Le passage à la plateforme S-Aloha ([ADR-0003](adr-0003-plateforme-modulaire-par-processus-itil.md))
et la refonte visuelle ([ADR-0005](adr-0005-langage-visuel-a-jetons-en-css-natif.md)) ont
posé la question d'une réécriture sur un stack JavaScript de bout en bout : Next.js,
Fastify, Drizzle, Tailwind, déploiement Kubernetes.

Le stack en place est : ASP.NET Core 8 + Entity Framework Core, React 18 + Vite,
PostgreSQL 16, authentification Active Directory on-prem (LDAP → JWT), déploiement Docker
Compose. Il fonctionne, et la cible de déploiement est un SI d'entreprise on-prem, adossé
à un Active Directory.

## Décision

**On conserve le stack existant.** La refonte porte sur l'organisation du code, la
documentation et le design, pas sur les technologies.

## Conséquences

- La restructuration modulaire se fait à stack constant : routes d'API, schéma et
  dépendances inchangés.
- Le nouveau design est réalisé en CSS natif à jetons, sans framework CSS — voir
  [`frontend.md`](../reference/frontend.md#design).

## Alternatives envisagées

- **Réécriture Next.js / Fastify / Drizzle / Tailwind / Kubernetes** — écarté : réécriture
  complète d'une application qui fonctionne, sans bénéfice fonctionnel ; perte de
  l'intégration .NET native avec l'Active Directory ; Kubernetes est surdimensionné pour un
  déploiement on-prem à trois containers.
- **Ajouter Tailwind seul au frontend Vite** — écarté : une dépendance de build de plus
  pour un CSS déjà organisé en jetons ; les pages existantes utilisent des styles en ligne
  et des classes maison qu'il aurait fallu réécrire.
