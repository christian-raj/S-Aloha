# ADR-0011 — Le projet se finance par le sponsoring et les services, en commençant par GitHub Sponsors

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

Pour durer, S-Aloha a besoin de moyens : temps de maintenance, hébergement d'une démo en
ligne, développement de la feuille de route. Le modèle reste 100 % AGPLv3, sans double
licence ([ADR-0010](adr-0010-certificat-d-origine-dco.md)) : la vente de licences est
exclue. La [stratégie communauté](../strategie-communaute.md#axe-4--financer-le-projet)
(décision D4) recense les canaux possibles.

## Décision

1. **GitHub Sponsors** est le premier canal : dons ponctuels ou récurrents, niveaux avec
   contreparties (nom dans les notes de version, logo dans le README et le wiki, vote sur
   la feuille de route, point mensuel). Le bouton « Sponsor » du dépôt
   (`.github/FUNDING.yml`) est publié **une fois le profil Sponsors du mainteneur
   activé**.
2. **Open Collective** s'ajoute quand une entreprise demande une facture et une dépense
   transparente.
3. **Sponsoring de fonctionnalités** et **offre de services** (installation, durcissement,
   formation) complètent : le résultat d'un sponsoring de fonctionnalité est toujours
   publié sous AGPLv3, pour tous.

## Conséquences

- Aucune fonctionnalité n'est réservée aux sponsors : ils accélèrent la feuille de
  route, ils ne l'achètent pas.
- Les contreparties sont publiques et identiques pour tous à niveau égal.
- Activer Sponsors demande au mainteneur des informations bancaires et fiscales : c'est
  un geste hors du dépôt (plan d'action C6).

## Alternatives envisagées

- **Open Collective d'abord** — reporté : un hébergeur fiscal et des frais de gestion
  pour un projet sans encore aucun sponsor ; GitHub Sponsors est là où sont déjà les
  développeurs.
- **Open core** (fonctionnalités payantes fermées) — écarté : contraire à l'AGPLv3 du
  projet et au choix du DCO.
- **Dons uniquement, sans contreparties** — écarté : une entreprise justifie plus
  facilement un sponsoring qui lui apporte de la visibilité.
