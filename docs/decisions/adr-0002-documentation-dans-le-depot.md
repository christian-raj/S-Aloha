# ADR-0002 — La documentation vit dans le dépôt ; un correctif et sa documentation tiennent dans un seul commit

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

La première version de la documentation (`README.md`, `docs/rules.md`,
`docs/architecture.md`) a été écrite au fil des commits. Lors de la restructuration en
plateforme modulaire ([ADR-0003](adr-0003-plateforme-modulaire-par-processus-itil.md)),
elle décrivait encore l'arborescence plate `Controllers/`, `Services/`, `pages/` alors que
le code avait déjà basculé vers `Core/` et `Modules/` : la dérive commence dès que code et
documentation sont mis à jour séparément.

Une documentation tenue ailleurs que dans le dépôt du code (wiki, dépôt séparé) aggrave
ce phénomène : deux commits dans deux dépôts, sans lien entre eux, dont un seul est
souvent fait.

## Décision

- Toute la documentation vit sous **`docs/`**, dans ce dépôt, rangée par nature de
  document (voir [`docs/readme.md`](../readme.md)) : `reference/` (socle vivant),
  `decisions/` (ADR), `archives/` (constats datés figés), `plan-action.md` (chantiers).
- **Un correctif et sa mise à jour documentaire tiennent dans un seul commit**, revu
  ensemble. Un changement de règle métier, de route d'API, de configuration ou de
  structure qui ne met pas à jour le document de référence concerné est incomplet.
- La santé de la base documentaire (liens, ancres, chemins du dépôt cités) se contrôle par
  `python3 scripts/check-docs.py`.

## Conséquences

- Les commentaires de code peuvent citer la documentation (`docs/reference/frontend.md
  § Design`, numéro d'ADR) ; renommer un document ou un titre impose de mettre à jour ces
  renvois.
- Les captures d'écran (`docs/screenshots/`) font partie de la documentation : une
  évolution visible de l'interface les rend obsolètes et doit les régénérer.
- Contrainte acceptée : un commit de code est un peu plus long à produire.

## Alternatives envisagées

- **Wiki GitHub ou dépôt de documentation séparé** — écarté : deux dépôts, deux commits,
  donc exactement la dérive que cette décision veut empêcher.
- **Documentation générée depuis le code uniquement** (Swagger, commentaires XML) —
  insuffisant : Swagger décrit les routes, pas les règles métier ni le *pourquoi*. Swagger
  reste exposé en complément.
