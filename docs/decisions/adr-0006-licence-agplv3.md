# ADR-0006 — S-Aloha est publié sous licence AGPLv3

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

Le dépôt va devenir public. Sans licence explicite, le code est seulement consultable :
personne ne peut légalement l'utiliser, le modifier ni le redistribuer.

S-Aloha est une application **serveur** : on l'utilise à travers le navigateur, sans
jamais recevoir le programme. Avec une licence copyleft classique (GPL), un tiers pourrait
modifier S-Aloha, l'opérer comme service pour ses clients et garder ses modifications
fermées, puisqu'il ne « distribue » rien.

## Décision

S-Aloha est publié sous **GNU Affero General Public License v3, ou toute version
ultérieure** (SPDX `AGPL-3.0-or-later`), selon la formule recommandée par la FSF.

- Le texte intégral est dans `LICENSE`, à la racine.
- La licence est déclarée dans `frontend/package.json` et `backend/SAloha.Api.csproj`.
- L'interface affiche un lien **« Code source · AGPLv3 »** (page de connexion et pied de
  la barre latérale), pour satisfaire la section 13 : toute version modifiée mise à
  disposition par le réseau doit offrir son code source à ses utilisateurs. L'URL est
  configurable au build (`VITE_SOURCE_URL`), voir
  [`exploitation.md`](../reference/exploitation.md#licence-et-code-source).

## Conséquences

- Chacun peut utiliser, étudier, modifier et redistribuer S-Aloha, y compris dans un
  contexte commercial.
- Quiconque opère une **version modifiée** pour des utilisateurs à travers le réseau doit
  leur donner accès au code source de cette version, sous la même licence.
- Une organisation qui déploie S-Aloha **sans le modifier** n'a pas d'obligation
  supplémentaire : le lien vers le dépôt d'origine suffit.
- Les dépendances (ASP.NET Core, EF Core, Npgsql, Novell LDAP, Swashbuckle, React, React
  Router, Vite) sont sous licences permissives (MIT, Apache 2.0, PostgreSQL), compatibles
  avec l'AGPLv3. La police Jost est sous OFL et chargée depuis Google Fonts, non
  redistribuée.
- Toute nouvelle dépendance doit rester compatible avec l'AGPLv3.
- Les contributions extérieures sont acceptées sous la même licence. Une relicence
  ultérieure demanderait l'accord de tous les contributeurs.

## Alternatives envisagées

- **MIT / Apache 2.0** — écarté : autorise une reprise fermée offerte comme service, sans
  aucun retour pour le projet.
- **GPLv3** — écarté : ne couvre pas l'usage à travers le réseau, qui est précisément le
  mode d'utilisation de S-Aloha.
- **AGPLv3 « only »** (sans « ou toute version ultérieure ») — non retenu : empêche de
  bénéficier d'une future version corrigée de la licence. Reste possible : il suffit de
  remplacer `-or-later` par `-only` dans les déclarations et la mention de licence.
- **Licence « source disponible »** (BSL, SSPL) — écarté : pas une licence libre au sens
  de l'OSI ; elle freine l'adoption en entreprise.
