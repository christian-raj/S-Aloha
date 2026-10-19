# ADR-0016 — Réponses de l'API : des DTO, plus d'entités EF

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-19
- **Statut** : accepté le 2026-10-19
- **Décideur** : Christian Rajaonary

## Contexte

`RecordController` et les contrôleurs des pratiques renvoient les entités EF Core telles
quelles ; `ReferenceHandler.IgnoreCycles` évite les cycles de navigation, et quelques champs
internes sont masqués au cas par cas (`[JsonIgnore]`). Le contrat de l'API est donc couplé au
schéma de la base : un champ ajouté à une entité est exposé sans qu'on y pense, et une
navigation chargée pour un calcul part dans la réponse ([audit du 2026-10-16](../archives/audit-code-2026-10-16.md)
§ 5, chantier A2 du [plan d'action](../plan-action.md)).

## Décision

1. **Toutes les réponses** de l'API passent par des **DTO de réponse** explicites, pour les dix
   pratiques et le socle (registres, fiches, sous-objets, console, reporting déjà projetés).
   Le chantier est mené **maintenant**, d'un bloc, plutôt qu'au fil des évolutions.
2. Les DTO **gardent les noms et les formes actuels** des champs utiles (camelCase) : le
   frontend ne change pas, sauf pour les champs internes qui disparaissent.
3. Une entité EF n'est **jamais** sérialisée : les contrôleurs renvoient `ActionResult<TDto>`
   (types connus de la description OpenAPI) ; les navigations ne sortent que sous la forme d'un
   DTO résumé (référence, titre, statut).
4. **Contrat figé par un test** : un instantané des schémas de réponse de la description
   OpenAPI est versionné dans les tests ; toute différence fait échouer `OpenApiTests` et doit
   être acceptée en connaissance de cause dans la PR.

## Mise en œuvre (2026-10-19)

- `RecordController<T, TDto, TView>` et `ProjectPartController<T, TDto, TView>` : chaque
  pratique déclare `View(e)` ; les DTO de réponse sont dans `Models/Views.cs` de chaque module
  (et `Core/Perimeters/Views.cs`, `AuditEntryView`, `CommentView`).
- **Garde-fou** : `Core/Data/EntityGuard.cs`, convertisseur JSON de MVC, refuse tout type du
  modèle EF ; toute la suite de tests passe avec lui.
- **Instantané** : `OpenApiTests` vérifie qu'aucun schéma n'est une entité et fige les champs
  des vues typées (`openapi-reponses.json`) ; les réponses composites (console, tableau de
  bord, inventaire…) restent des projections explicites.

## Conséquences

- Chantier transversal : un fichier de DTO par pratique, une projection par entité, des
  contrôleurs typés. `IgnoreCycles` devient inutile mais reste sans effet.
- Les champs purement internes (JSON figés, données brutes de découverte, navigations
  inverses) disparaissent des réponses ; le frontend est vérifié pour chacun.
- Un nouveau champ exposé l'est par décision : il apparaît dans l'instantané OpenAPI.

## Alternatives envisagées

- **DTO progressifs**, pratique par pratique à l'occasion des évolutions — écarté par le
  décideur : le contrat resterait mixte pendant longtemps.
- **Statu quo** avec `[JsonIgnore]` au cas par cas — écarté : la protection dépend de la
  vigilance de chaque auteur.
