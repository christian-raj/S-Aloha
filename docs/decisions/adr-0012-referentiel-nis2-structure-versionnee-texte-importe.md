# ADR-0012 — Référentiel NIS 2 : structure versionnée dans le dépôt, texte importé par l'administrateur

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-08
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

S-Aloha ajoute une pratique d'évaluation de la conformité à la directive **NIS 2**
([conformité NIS 2](../reference/processus/conformite-nis2.md)). Le référentiel retenu est
le **Référentiel Cyber France (ReCyF)** de l'ANSSI, qui décline les mesures de l'article 21
de la directive : 20 objectifs, quatre piliers, 152 exigences, dans sa version 2.5 du
17 mars 2026, marquée « version de travail ».

Licence, vérifiée le 2026-10-08 :

- les mentions légales du site de l'ANSSI placent ses contenus sous **Licence Ouverte /
  Etalab 2.0**, **sauf** que « l'exploitation commerciale de ces contenus reste soumise à
  une autorisation préalable » ;
- le document du ReCyF ne porte aucune licence propre.

S-Aloha est sous **AGPLv3** ([ADR-0006](adr-0006-licence-agplv3.md)), qui autorise
l'usage commercial : y inclure le texte des exigences imposerait une restriction que la
licence du projet ne permet pas.

Les correspondances avec les mesures **ISO/IEC 27002:2022** posent une seconde question :
les intitulés des mesures ISO sont protégés par le droit d'auteur de l'ISO, et ces
correspondances ne proviennent pas de l'ANSSI.

## Décision

1. Le dépôt ne contient que la **structure factuelle** du référentiel, dans deux fichiers
   CSV embarqués dans l'API (`backend/Modules/ComplianceAssessment/Referential/`) :
   - `objectifs.csv` : numéro, titre et pilier des 20 objectifs, d'après les tableaux de
     correspondance publiés par l'ANSSI ;
   - `exigences.csv` : code de chaque exigence (ex. `5.B.4-EI/EE`), objectif, thématique,
     cibles (EI/EE ou EE) et **numéros** des mesures ISO 27002:2022, **sans intitulés**,
     présentés comme **indicatifs**.
2. Le **texte des exigences n'est pas dans le dépôt** : chaque organisation l'importe
   depuis le document de l'ANSSI, par un import CSV réservé à l'administrateur (section
   Paramétrage), qu'elle réalise sous sa propre responsabilité au regard des conditions
   de l'ANSSI.
3. La structure est **versionnée avec le code** : à chaque démarrage, l'API aligne la base
   sur les CSV embarqués (ajouts, mise à jour des thématiques, cibles et mesures). Cet
   alignement **n'écrase jamais le texte importé** et ne supprime pas une exigence retirée,
   que des réponses peuvent viser.
4. Une nouvelle version du ReCyF se traite comme une évolution du code : CSV mis à jour,
   version déclarée dans `ReferentialStore.Version`, règles de la pratique et journal des
   versions mis à jour. Pour la v2.5 : exigence 13.5 ciblée EI/EE, 14.5 ciblée EE.

## Conséquences

- Le dépôt reste entièrement sous AGPLv3 ; aucune donnée sous condition d'usage n'y entre.
- **La base ne contient pas le texte à l'installation.** Tant qu'il n'est pas importé, le
  questionnaire affiche chaque exigence par son **identifiant et ses mesures ISO**, avec un
  message qui renvoie vers l'import : l'évaluation est possible, mais moins lisible. Le
  signalement des exigences sans texte à l'Admin est prévu
  ([NIS-23](../reference/processus/conformite-nis2.md)).
- Le format d'import est tolérant (séparateur `;` ou `,`, colonnes « Référence »/« Code »
  et « Contenu »/« Texte », BOM, champs multilignes) pour accepter une extraction du
  document de l'ANSSI sans retraitement lourd.
- Le ReCyF étant une version de travail, la structure évoluera : le versionnement par le
  code garde la trace de chaque version (historique git, journal des versions).
- Les correspondances ISO restent indicatives : elles aident un lecteur qui connaît
  ISO 27002, sans engager ni l'ANSSI ni l'ISO.

## Alternatives envisagées

- **Embarquer le texte complet** — écarté : restriction d'usage commercial incompatible
  avec l'AGPLv3, et risque juridique pour toute organisation qui revend un service fondé
  sur S-Aloha.
- **Demander une autorisation à l'ANSSI** — non retenu à ce stade : délai incertain, et
  la structure suffit au fonctionnement. Reste possible : une autorisation obtenue
  permettrait d'embarquer le texte par un nouvel ADR.
- **Télécharger le texte automatiquement depuis le site de l'ANSSI** — écarté : dépendance
  à un format de publication non stable (PDF), et même question de licence pour la copie.
- **Saisie manuelle des exigences par chaque organisation** — écarté : 152 exigences à
  ressaisir, avec un risque d'erreur sur les codes et les cibles, qui sont précisément ce
  que la structure embarquée garantit.
