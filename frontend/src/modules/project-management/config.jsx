import { api } from '../../api'
import { titleField, descriptionField, ownerField, dateFr, dateTimeFr } from '../../core/fields'

export const METHODS = ['Scrum', 'Kanban', 'Cycle en V', 'PRINCE2', 'Hybride']

/** Composants (PRJ-03) : libellé et rôle, pour le choix d'un projet hybride. */
export const COMPONENTS = [
  { id: 'backlog', label: 'Backlog', hint: 'User stories, tâches et bogues priorisés' },
  { id: 'sprints', label: 'Sprints', hint: 'Itérations à objectif, une active à la fois' },
  { id: 'board', label: 'Tableau', hint: 'Colonnes À faire → Terminé' },
  { id: 'burndown', label: 'Burndown', hint: 'Points restants du sprint, jour par jour' },
  { id: 'phases', label: 'Phases', hint: 'Phases séquentielles (cycle en V)' },
  { id: 'milestones', label: 'Jalons', hint: 'Échéances clés' },
  { id: 'deliverables', label: 'Livrables', hint: 'Produits attendus, validés par le chef de projet' },
  { id: 'stages', label: 'Étapes', hint: 'Étapes de gestion PRINCE2, validation de fin d\'étape' },
  { id: 'business-case', label: 'Business case', hint: 'Justification de l\'affaire' },
  { id: 'risks', label: 'Risques', hint: 'Registre des risques' },
  { id: 'issues', label: 'Incidents projet', hint: 'Demandes de changement, hors spécification, préoccupations' },
  { id: 'tolerances', label: 'Tolérances', hint: 'Délai et coût au-delà desquels on remonte une exception' },
]

/** Composants imposés par une méthode (miroir de Methods.DefaultComponents côté API). */
export const DEFAULT_COMPONENTS = {
  Scrum: ['backlog', 'sprints', 'board', 'burndown'],
  Kanban: ['backlog', 'board'],
  'Cycle en V': ['phases', 'milestones', 'deliverables'],
  PRINCE2: ['stages', 'business-case', 'risks', 'issues', 'tolerances'],
  Hybride: ['phases', 'milestones', 'deliverables', 'backlog', 'sprints', 'board', 'burndown', 'risks'],
}

export const componentsOf = (project) => (project.components || '').split(',').filter(Boolean)

const money = (v) => (v === null || v === undefined ? '—' : Number(v).toLocaleString('fr-FR', { maximumFractionDigits: 2 }))

// Gestion de projet : la méthode active des composants, et les composants
// décident des onglets de la fiche (docs/reference/processus/gestion-de-projet.md).
export const projectConfig = {
  title: 'Projets',
  sub: 'Suivi des projets selon leur méthode : Scrum, Kanban, cycle en V, PRINCE2 ou hybride.',
  api: api.projects, basePath: '/projects', linkType: 'project',
  createLabel: '+ Nouveau projet', createTitle: 'Proposer un projet',
  empty: 'Aucun projet.', ownerLabel: 'Que je pilote',
  statuses: [
    { value: 'Cadrage', tone: 'new' }, { value: 'En cours', tone: 'progress' },
    { value: 'Suspendu', tone: 'wait' }, { value: 'Clôturé', tone: 'done' }, { value: 'Abandonné', tone: 'closed' },
  ],
  fields: [
    titleField('Intitulé', 'Ex. : Refonte du portail RH'),
    { key: 'method', label: 'Méthode', type: 'select', options: METHODS, required: true, default: 'Scrum' },
    ownerField('Chef de projet'),
    { key: 'startDate', label: 'Début', type: 'date' },
    { key: 'targetEndDate', label: 'Fin cible', type: 'date' },
    { key: 'budget', label: 'Budget', type: 'number' },
    { key: 'actualCost', label: 'Coût réel', type: 'number', create: false },
    descriptionField('Objectif et périmètre', 'Ce que le projet doit produire, pour qui…', 3),
    { key: 'abandonReason', label: 'Motif d\'abandon', type: 'textarea', rows: 2, create: false,
      placeholder: 'Obligatoire pour abandonner le projet.' },
  ],
  // Champs édités hors du formulaire Informations, renvoyés tels quels.
  keep: (r) => ({
    components: componentsOf(r), justification: r.justification, expectedBenefits: r.expectedBenefits,
    expectedCosts: r.expectedCosts, majorRisks: r.majorRisks, toleranceDays: r.toleranceDays,
    toleranceCostPercent: r.toleranceCostPercent, toleranceScope: r.toleranceScope, toleranceQuality: r.toleranceQuality,
  }),
  columns: [
    { h: 'Méthode', v: r => r.method },
    { h: 'Chef de projet', v: r => r.ownerDisplayName || '—' },
    { h: 'Fin cible', v: r => dateFr(r.targetEndDate) },
  ],
  meta: r => [r.method, r.validatedAt && `Validé par ${r.validatedBy} le ${dateTimeFr(r.validatedAt)}`,
    r.budget !== null && r.budget !== undefined && `Budget ${money(r.budget)}`],
  help: 'Le chef de projet propose le projet (Cadrage) ; un gestionnaire le valide en le passant « En cours ». Clôturé et Abandonné sont définitifs.',
  linkHint: 'Changements livrés par le projet, problèmes qu\'il corrige, améliorations qu\'il porte, CI et services concernés…',
}
