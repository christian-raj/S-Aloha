# 📊 Reporting

> Tableau de bord des pratiques (page **Reporting**, `/reports`) : santé des services,
> incidents, problèmes, changements et projets, conformité. Capacité transverse du socle
> (`backend/Core/Pilotage/DashboardController.cs`, `frontend/src/core/pages/Reporting.jsx` et
> `reporting/`), pas une pratique ITIL. Préfixe des règles : **REP**. Conventions (✅ / 🔜,
> lots) : [règles métier](regles-metier.md#3-conventions-de-ce-référentiel). Indicateurs
> propres à chaque pratique : § 8 de son document.

<sub>[← Documentation](../readme.md) · [Produit](produit.md) · [Règles métier](regles-metier.md) · [Architecture](architecture.md) · [Base de données](base-de-donnees.md) · [Frontend](frontend.md) · [Exploitation](exploitation.md) · [Sécurité](securite.md) · [Glossaire](glossaire.md)</sub>

## 1. Objectif

Donner en un écran l'état du service rendu et ce qui demande une action : un score de
santé expliqué, les incidents à suivre, les causes racines en attente, les changements et
projets à risque, la conformité, l'état de la CMDB. Chaque onglet se lit seul et s'exporte.

## 2. Filtres et onglets

Filtres communs, conservés dans l'URL (`?tab=&period=&from=&to=&service=&perimeter=`) :

- **Période** : 7, 30 (défaut), 90 jours, 12 mois ou personnalisée ; bornes **incluses**,
  aujourd'hui compris ([SOC-18](regles-metier.md#7-reporting)).
- **Service** : nom d'un service du catalogue. Il filtre les **incidents** et les
  **problèmes** (service affecté), pas les changements ni les projets, qui n'ont pas de
  service ; les onglets Changements & projets et Configuration & inventaire l'ignorent.
- **Périmètre** ([PER-09](perimetres.md)) : les indicateurs portent sur les périmètres de
  l'utilisateur (tous pour l'Admin) ; le filtre, affiché dès qu'on en a plusieurs, en retient
  un. Un périmètre qui n'est pas le sien ne renvoie rien.

| Onglet | API | Règles |
|---|---|---|
| Synthèse globale | `GET /api/reports/dashboard/overview` | REP-01 à REP-07 |
| Incidents | `GET /api/reports/dashboard/incidents` | REP-10 à REP-15 |
| Problèmes | `GET /api/reports/dashboard/problems` | REP-20 à REP-25 |
| Changements & projets | `GET /api/reports/dashboard/changes-projects` | REP-30 à REP-34 |
| Conformité & métriques | `GET /api/reports/dashboard/compliance` | REP-40 à REP-43 |
| Configuration & inventaire | `GET /api/reports/dashboard/configuration` | REP-50 à REP-53 |

Paramètres : `from`, `to` (dates, incluses), `service` (facultatif, sans tenir compte de la
casse). Policy : User. L'ancien point d'entrée `GET /api/reports/summary` reste servi.

## 3. Synthèse globale

| ID | Règle | Statut | Lot |
|---|---|---|---|
| REP-01 | **Score de santé** = 100 − pénalités, plancher 0, formule affichée sous le score : incidents majeurs ouverts −25 chacun (50 au plus), P1 ouverts non majeurs −10 (30 au plus), incidents ouverts hors SLA −5 (25 au plus), changements en échec clos sur la période −5 (20 au plus), projets hors tolérance de délai ou de coût −10 (20 au plus). **Bon** ≥ 80, **Dégradé** 50 à 79, **Critique** < 50 | ✅ | |
| REP-02 | Indicateurs : incidents majeurs ouverts (en alerte), **taux de résolution dans le SLA** sur la période (incidents résolus ayant une échéance, [INC-21](processus/gestion-des-incidents.md)), changements planifiés **cette semaine** (lundi à dimanche, hors rejetés), **projets à risque** (tolérance À surveiller ou Hors tolérance, [PRJ-31, PRJ-32](processus/gestion-de-projet.md)) | ✅ | |
| REP-03 | **Volumétrie** par jour de la période : incidents et demandes créés, changements mis en œuvre (début planifié des changements Planifié, Mis en œuvre ou Clos) | ✅ | |
| REP-04 | **Matrice des priorités** des incidents et problèmes ouverts : P1 Critique, P2 Élevée, P3 Moyenne, P4 Basse | ✅ | |
| REP-05 | **État de tolérance** d'un projet (délai, coût) : même calcul que la synthèse du projet ([PRJ-31, PRJ-32](processus/gestion-de-projet.md)) | ✅ | |
| REP-06 | **Projets à risque ou en retard** : liste avec leur état de tolérance et le lien vers la fiche | ✅ | |
| REP-07 | **Badges** des onglets : Incidents = majeurs ouverts + ouverts hors SLA (rouge s'il y a un majeur) ; Problèmes = RCA en attente (REP-22) ; Changements & projets = projets hors tolérance + changements en échec (rouge s'il y a un projet hors tolérance) ; Conformité = SLA à revoir ; Configuration = actifs matériels en fin de support dépassée + actifs du sas de staging à rapprocher ou en conflit, pour les gestionnaires (rouge s'il y a une fin de support dépassée) | ✅ | |
| REP-08 | **Exports** : CSV de l'onglet actif (séparateur « ; », BOM UTF-8, décimales à la française : s'ouvre tel quel dans Excel) ; **PDF** par l'impression du navigateur, tous les onglets chargés puis imprimés à la suite | ✅ | |

## 4. Incidents

| ID | Règle | Statut | Lot |
|---|---|---|---|
| REP-10 | **MTTR** : moyenne en heures de `ResolvedAt − CreatedAt` des incidents résolus sur la période | ✅ | |
| REP-11 | **MTTD** : moyenne en heures de `CreatedAt − OccurredAt` des incidents déclarés sur la période qui ont un début constaté ([INC-27](processus/gestion-des-incidents.md)), avec le nombre d'incidents mesurés | ✅ | |
| REP-12 | **FCR** : part des incidents résolus au premier contact ([INC-23](processus/gestion-des-incidents.md)) parmi les résolus de la période | ✅ | |
| REP-13 | **Backlog par âge** des incidents ouverts : < 24 h, 1 à 7 jours, > 7 jours | ✅ | |
| REP-14 | **Burn-up** : cumul jour par jour des incidents créés et résolus depuis le début de la période ; répartition des créés par catégorie et par service | ✅ | |
| REP-15 | **Incidents à suivre** : ouverts majeurs, **SLA en risque** (moins de 25 % du délai restant, [INC-24](processus/gestion-des-incidents.md)) ou **SLA dépassé** ; majeurs d'abord, puis par échéance | ✅ | |

## 5. Problèmes

| ID | Règle | Statut | Lot |
|---|---|---|---|
| REP-20 | Problèmes ouverts et **erreurs connues** | ✅ | |
| REP-21 | **Incidents par problème** : nombre moyen d'incidents reliés | ✅ | |
| REP-22 | **RCA en attente** : problème En analyse dont une analyse a conclu, sans cause racine validée sur le problème | ✅ | |
| REP-23 | **Top 5 des services** qui génèrent le plus de problèmes, avec leurs incidents reliés | ✅ | |
| REP-24 | **Entonnoir** : Identifié (Nouveau), RCA en cours (En analyse), Contournement trouvé (Erreur connue sans correctif planifié), Correctif planifié (ouvert, avec une action ouverte ou un changement relié), Clôturé (Résolu ou Clos) | ✅ | |
| REP-25 | **Actions correctives** : ouvertes, en retard (avec leurs responsables R), terminées sur la période | ✅ | |

## 6. Changements et projets

| ID | Règle | Statut | Lot |
|---|---|---|---|
| REP-30 | **Taux de succès** des changements dont le résultat est renseigné, clos sur la période | ✅ | |
| REP-31 | Changements **par type** (Standard, Normal, Urgent) créés sur la période | ✅ | |
| REP-32 | **Avancement** des projets En cours ou Suspendus : points terminés, sinon phases ou étapes terminées, sinon livrables validés ; moyenne des projets mesurables | ✅ | |
| REP-33 | **Calendrier** des changements des 30 prochains jours (hors rejetés et clos), en Gantt | ✅ | |
| REP-34 | **Matrice 5 × 5** des risques ouverts des projets actifs (probabilité × impact) | ✅ | |

## 7. Conformité et métriques

| ID | Règle | Statut | Lot |
|---|---|---|---|
| REP-40 | Dernière **évaluation NIS 2 validée** : score, maturité, score par pilier, écarts, actions d'amélioration reliées ([NIS-10 à NIS-12](processus/conformite-nis2.md)), calculés sur ses exigences figées ([NIS-18](processus/conformite-nis2.md)). Seule une évaluation Validée donne un score ; les évaluations non validées sont listées à part (référence, statut, lien vers la fiche), sans score | ✅ | |
| REP-41 | **Respect des SLA par service** (incidents résolus sur la période ayant une échéance) et **SLA à revoir** (En vigueur, date de revue passée) | ✅ | |
| REP-42 | **Amélioration continue** : registre par statut, améliorations réalisées sur la période | ✅ | |
| REP-43 | **Connaissances** : articles publiés, brouillons, revues en retard, publications de la période | ✅ | |
| REP-44 | **Déclarations réglementaires** (échéances tenues, en cours) ajoutées avec leur module ([DRG](processus/declarations-reglementaires.md)) | 🔜 | 2 |

## 8. Configuration et inventaire

État de la CMDB **à date** (CI non retirés de mes périmètres) : la période ne règle que la
courbe de la découverte. Le sas de staging n'est montré qu'aux gestionnaires, sur les
périmètres qu'ils gèrent ([CFG-40](processus/gestion-de-la-configuration.md)).

| ID | Règle | Statut | Lot |
|---|---|---|---|
| REP-50 | **Répartitions de la CMDB** : CI par type, par environnement, par statut ; applications par criticité (« Non renseignée » comprise) ([CFG-31](processus/gestion-de-la-configuration.md)) | ✅ | |
| REP-51 | **Couverture du portefeuille applicatif** : part des applications avec code, direction, propriétaire métier, responsable DSI et criticité ([CFG-35, CFG-43](processus/gestion-de-la-configuration.md)) | ✅ | |
| REP-52 | **Obsolescence** : actifs matériels par état de support, actifs en fin de support dépassée ou dans les 6 mois (25 au plus, du plus ancien au plus récent), avec la source de la date ([CFG-38, CFG-47](processus/gestion-de-la-configuration.md)) ; en alerte s'il y en a une dépassée | ✅ | |
| REP-53 | **Découverte** (gestionnaires) : actifs à rapprocher, en conflit, inconnus (Shadow IT, 25 derniers listés), rapprochés ; actifs par source ; nouveaux actifs et rapprochements par jour de la période ([CFG-36, CFG-37, CFG-42](processus/gestion-de-la-configuration.md)) | ✅ | |

## 9. Limites connues

- Les **heures** des échéances SLA sont calendaires ; heures de service et suspension En
  attente viendront avec [INC-22](processus/gestion-des-incidents.md).
- Le service d'un incident est retrouvé **par son nom** dans le catalogue en attendant
  INC-20.
- Les indicateurs se calculent en mémoire sur l'ensemble des incidents et problèmes
  filtrés : adapté aux volumes d'une DSI, à revoir au-delà de quelques dizaines de
  milliers d'enregistrements.
