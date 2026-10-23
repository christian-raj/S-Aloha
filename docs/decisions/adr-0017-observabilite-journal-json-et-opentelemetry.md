# ADR-0017 — Observabilité : journal JSON, OpenTelemetry optionnel

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-19
- **Statut** : accepté le 2026-10-19
- **Décideur** : Leipa

## Contexte

L'API écrit le journal texte par défaut d'ASP.NET Core ; ni journal structuré, ni traces, ni
métriques ([audit du 2026-10-16](../archives/audit-code-2026-10-16.md) § 10, chantier O2 du
[plan d'action](../plan-action.md)). En exploitation (Docker, Kubernetes), le journal est lu par
un collecteur (Loki, ELK, `kubectl logs`) qui attend une ligne JSON par événement ; les appels
lents ou en échec (annuaire, embeddings, découverte) ne se diagnostiquent qu'à l'aveugle.

## Décision

1. **Journal JSON** sur la sortie standard hors développement (une ligne par événement,
   horodatage UTC, niveau, catégorie, message, exception, portées) ; texte lisible en
   développement. Réglage `Logging:Format` (`json` ou `text`) pour forcer l'un ou l'autre.
2. **Corrélation** : chaque ligne d'une requête porte l'identifiant de trace (W3C
   `traceparent`), renvoyé au client dans l'en-tête `traceparent` de la réponse ; un incident
   signalé par un utilisateur se retrouve dans le journal.
3. **OpenTelemetry optionnel** : traces et métriques (requêtes HTTP entrantes, appels HTTP
   sortants, PostgreSQL, annuaire, découverte, notifications, embeddings) exportées en OTLP
   **seulement si** `OTEL_EXPORTER_OTLP_ENDPOINT` est défini ; sans collecteur, rien n'est
   envoyé ni retenu.
4. Jamais de secret ni de donnée personnelle au-delà de l'identifiant du compte dans le
   journal et les traces (mots de passe, jetons, secrets du coffre, contenu des fiches exclus).

## Conséquences

- Dépendances : paquets OpenTelemetry officiels (.NET et Npgsql), versions épinglées.
- Instrumentation explicite des appels propres à S-Aloha (`ActivitySource` et `Meter`
  « SAloha »).
- Documentation d'exploitation : format du journal, variables OpenTelemetry.

## Alternatives envisagées

- **Journal JSON seulement** — écarté : les lenteurs de l'annuaire ou de la découverte restent
  invisibles.
- **Serilog ou un autre cadre de journalisation** — écarté : le formateur JSON intégré à .NET
  suffit et n'ajoute pas de dépendance.
