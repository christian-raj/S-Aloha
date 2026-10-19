# ADR-0018 — Jeton de session en cookie HttpOnly, politique CSP

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-19
- **Statut** : accepté le 2026-10-19
- **Décideur** : Christian Rajaonary

## Contexte

Le jeton JWT (8 h) est rangé dans le `sessionStorage` du navigateur : tout script exécuté
dans la page peut le lire et l'envoyer ailleurs (XSS). nginx ne sert aucune politique de
sécurité du contenu (CSP). Chantier S5 du [plan d'action](../plan-action.md) ; constat
initial : [état du 2026-10-06](../archives/etat-initial-2026-10-06.md).

## Décision

1. À la connexion, l'API pose le jeton dans un **cookie** `HttpOnly`, `SameSite=Strict`,
   limité à `/api`, d'une durée égale à celle du jeton ; `Secure` dès que la requête arrive
   en HTTPS (directement ou derrière le proxy, `X-Forwarded-Proto`). Le jeton n'est plus
   renvoyé dans le corps de la réponse ni stocké par le frontend.
2. **Protection CSRF** : en plus de `SameSite=Strict`, une requête qui modifie (POST, PUT,
   PATCH, DELETE) authentifiée par le cookie doit porter l'en-tête `X-S-Aloha: 1`, qu'un site
   tiers ne peut pas ajouter sans autorisation CORS ; sinon 403.
3. **Clients d'API** (scripts, tests, intégrations) : l'en-tête `Authorization: Bearer`
   reste accepté, sans exigence CSRF (un navigateur ne l'envoie jamais de lui-même).
4. **Déconnexion** : `POST /api/auth/logout` efface le cookie ; `GET /api/auth/me` rend
   l'utilisateur de la session, pour que l'interface se recharge sans stocker de secret.
5. **CSP** servie par nginx : scripts de la seule origine (le script de thème en ligne
   autorisé par son empreinte), polices Google Fonts, aucun cadre (`frame-ancestors 'none'`),
   aucun `object`, connexions vers la seule origine.

## Conséquences

- Chacun se reconnecte une fois à la mise à jour (le jeton du `sessionStorage` est oublié).
- En HTTP seul (compose par défaut, évaluation), le cookie n'est pas `Secure` : HTTPS (S2)
  reste exigé en production.
- Le mode démonstration et les tests d'API ne changent pas de comptes.

## Alternatives envisagées

- **`sessionStorage` et CSP** — écarté : une faille XSS lirait toujours le jeton.
- **Session côté serveur** (cookie opaque, état en base) — écarté pour l'instant : le JWT reste
  la seule source des rôles et des groupes AD, sans table de sessions à maintenir.
