# Revue fonctionnelle — gestion des problèmes et console d'administration, 2026-10-06

<sub>[← Documentation](../readme.md)</sub>

> **Document d'archive, figé.** Constat daté, établi sur le commit `1be5879`. Il n'est
> pas mis à jour : les corrections iront dans le code, ce qui reste vrai dans
> [`reference/`](../reference/), ce qui reste à faire dans
> [`plan-action.md`](../plan-action.md).

## Périmètre et méthode

- **Module Gestion des problèmes** : déclaration, qualification, cycle de vie, analyses
  de cause racine (5 Pourquoi, Ishikawa, FTA), actions correctives et RACI, suivi
  transverse, reporting.
- **Console** : « Ma console » par rôle, et ce que l'application offre à un
  Administrateur.

Deux temps :

1. **Lecture** du code backend (`backend/Modules/ProblemManagement/`, `backend/Core/`)
   et frontend (`frontend/src/modules/problem-management/`, `frontend/src/core/`),
   confrontée à [`regles-metier.md`](../reference/regles-metier.md).
2. **Exécution** : API réelle sur une base PostgreSQL jetable, jetons signés pour les
   trois rôles (l'AD n'est pas joignable sur le poste de revue), 15 scénarios scriptés
   sur l'API et parcours dans un navigateur (Chromium headless).

Chaque constat porte sa preuve : **[exécuté]** = reproduit, **[lecture]** = établi à la
lecture du code seulement.

## Synthèse

| Gravité | Nombre | En une phrase |
|---|---|---|
| 🔴 Bloquant | 3 | On ne peut créer aucune analyse depuis l'interface, et la déclaration de problèmes se bloque définitivement après une suppression |
| 🟠 Majeur | 8 | FTA inutilisable, « Mes actions » incomplet ou vide selon la casse ou les groupes, MTTR faux, pas d'édition ni de suppression dans l'interface |
| 🟡 Règle non tenue | 6 | L'API accepte des statuts inventés, une RACI vide en modification, une clôture avec des actions ouvertes |
| ⚪ Manque | 1 | **Il n'existe pas de console d'administration** |

Ce qui fonctionne et a été vérifié : les droits par rôle sur l'API (User, Manager,
Admin : 403 aux bons endroits), le calcul de priorité impact × urgence, la composition
de la console côté serveur et l'ordre « À traiter » / « À suivre », la consultation et
l'édition d'une analyse 5 Pourquoi existante, le reporting.

## 🔴 Bloquants

### B1 — Impossible de créer une analyse depuis l'interface [exécuté]

Un clic sur « + 5 Pourquoi », « + Ishikawa » ou « + Arbre des défaillances » fait
planter l'onglet : écran vide, `SyntaxError: "undefined" is not valid JSON`.

Cause : `ProblemDetail.jsx`, composant `Rca`,
`useState(current ? JSON.parse(current.dataJson) : {})`. L'argument est réévalué à
**chaque rendu** ; une analyse qu'on démarre (`{ method, id: null }`) n'a pas de
`dataJson`. Cela touche la première analyse d'un problème comme les suivantes.
L'étape « Investigation et diagnostic » du processus est donc inaccessible ; les
analyses visibles sur les captures avaient été créées par l'API.

### B2 — Après la suppression d'un problème, plus aucune déclaration possible [exécuté]

Référence = `PRB-AAAA-` + (nombre de problèmes de l'année + 1). Si l'on supprime
PRB-2026-0002 sur trois, le calcul redonne 0003, qui existe déjà : l'index unique
répond 500. **Toutes** les créations suivantes échouent, sans limite de temps
(constaté : 500, 500, 500). Seul un Admin peut supprimer, et il n'a pas d'interface
pour le faire (M8) : aujourd'hui, le blocage ne survient que par l'API.

### B3 — Des déclarations simultanées échouent [exécuté]

Sur une base propre, 5 déclarations simultanées : **1 réussit, 4 répondent 500**
(même calcul de référence, même index unique). Probable dès que plusieurs personnes
déclarent en même temps.

## 🟠 Majeurs

### M1 — Arbre des défaillances (FTA) : la saisie perd le focus à chaque caractère [exécuté]

On tape « Disque plein » dans une cause, le champ ne garde que « D ». Le composant
`Node` est défini **dans** le rendu de `FtaTree` ; à chaque frappe, React le recrée et
démonte le champ. La méthode est inutilisable.

### M2 — « Mes actions » dépend de la casse de l'identifiant saisi [exécuté]

Le jeton porte l'identifiant **tel que tapé** au login (`AuthController`), pas le
`sAMAccountName` de l'annuaire, alors que l'AD ignore la casse. Connecté en
`Tiana.Ravelo` au lieu de `tiana.ravelo` : « Mes actions » passe de 1 à **0**. Même
effet sur « Mes problèmes déclarés », le filtre « Mes affectations » et le
`CreatedBy` des nouveaux problèmes.

### M3 — Les actions confiées à un groupe AD n'apparaissent chez personne [exécuté]

« Mes actions » ne compare que `AssigneeId == identifiant`. Une action dont le R est
un groupe (`GRP-RESEAU`) n'apparaît dans la console d'aucun membre : le jeton ne porte
pas les groupes, la console n'a aucun moyen de le savoir. Or le sélecteur RACI propose
explicitement les groupes.

### M4 — La réouverture d'un problème clos fausse le MTTR [exécuté]

Passer de « Clos » à « En analyse » conserve `ClosedAt`. Le reporting calcule alors un
MTTR (0 j) **alors qu'aucun problème n'est clos**, et un problème rouvert continue de
compter comme résolu.

### M5 — Recherche sensible à la casse [exécuté]

`q=alpha` → 0 résultat, `q=Alpha` → 1 (`Contains` traduit en `LIKE` sous PostgreSQL).

### M6 — Une action à échéance du jour est « en retard » dès le matin [exécuté]

L'échéance saisie (date seule) est stockée à minuit UTC, puis comparée à « maintenant ».
Une action due aujourd'hui est signalée en retard dès 3 h, heure de Madagascar, et
remonte en tête de la console.

### M7 — Pas d'édition d'une action existante [lecture]

Seul le statut se modifie dans l'interface : ni le titre, ni l'échéance, ni la RACI.
Pour replanifier une action en retard (ce que la console demande), il faut la recréer.

### M8 — Aucune suppression dans l'interface [exécuté]

L'API autorise la suppression d'analyses et d'actions (Manager) et de problèmes (Admin),
mais aucun bouton n'existe, y compris pour un Admin. Les doublons et erreurs de saisie
ne peuvent pas être nettoyés.

## 🟡 Règles métier que l'API ne tient pas

| # | Constat | Preuve |
|---|---|---|
| R1 | Statut, impact, urgence, méthode d'analyse et rôle RACI sont des chaînes libres : `status: "Statut inventé"`, `impact: "Énorme"` → **200**, priorité silencieusement à P4 | [exécuté] |
| R2 | Règles RACI (au moins un R, exactement un A) vérifiées **à la création seulement** : modification avec une RACI vide → **200** | [exécuté] |
| R3 | La date d'achèvement d'une action terminée est réécrite à chaque enregistrement (simple renommage → nouvelle date) | [exécuté] |
| R4 | Cycle de vie non contraint : clôture d'un problème qui a 3 actions ouvertes → **200** ; tout retour arrière est permis (Clos → Nouveau) | [exécuté] |
| R5 | Tout User peut annuler ou réaffecter l'action d'un autre (→ 200), conformément aux règles actuelles, mais **sans aucun historique** : ni qui, ni quand, ni l'ancienne valeur | [exécuté] |
| R6 | AD injoignable : la recherche dans l'annuaire répond 500 (sélecteur RACI vide sans message) ; au login, l'erreur de connexion est avalée et l'utilisateur lit « identifiants invalides » | 500 [exécuté] ; login [lecture] |

## ⚪ Console d'administration

**Elle n'existe pas.** La console d'un Admin est **identique** à celle d'un
Gestionnaire : même requête côté serveur (`role is "Manager" or "Admin"`), mêmes
blocs, et la navigation n'a aucune entrée d'administration. Être Admin ne donne que le
droit de supprimer un problème par l'API (B2, M8).

Ce qu'une console d'administration devrait couvrir, au vu des constats ci-dessus :

- **Accès** : qui a quel rôle, et contrôle du mapping groupes AD → rôles, aujourd'hui
  modifiable seulement dans `appsettings.json` ou les variables d'environnement.
- **Santé** : base, AD joignable ou non (R6), version déployée.
- **Référentiels** : catégories et services affectés, aujourd'hui en texte libre. Le
  reporting par catégorie se fragmente dès la première faute de frappe (« Réseau » /
  « reseau »).
- **Journal d'audit** : qui a changé quoi et quand (R5), suppressions comprises.
- **Nettoyage** : suppression et fusion de doublons (M8).

## Hygiène relevée en passant

- `puppeteer-core` figure dans les `dependencies` du frontend, ajouté au commit
  `b4121bc` pour les captures, et n'est importé nulle part : il alourdit
  `npm install` dans l'image Docker.
- Aucun test automatisé, ni backend ni frontend : les quinze scénarios de cette revue
  n'existent que dans un script jetable.

## Ordre de correction proposé

1. **B1, B2, B3** — le processus est bloqué. Correctifs courts : évaluation paresseuse
   (ou initialisation dans `start`) côté `Rca` ; référence tirée d'une séquence
   PostgreSQL (ou du maximum existant + 1, sous verrou). À fermer avec un test chacun.
2. **M1, M2, M4, M5, M6** — correctifs locaux, sans décision à prendre.
3. **R1 à R4** — validation côté API ; R4 demande de trancher les transitions permises
   (règle métier à écrire avant le code).
4. **M3, M7, M8, R5 et la console d'administration** — chantiers qui demandent une
   décision : groupes dans le jeton ou résolution à la volée, historique des
   modifications, périmètre de la console d'administration.
