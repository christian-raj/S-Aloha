import { api } from '../../api'
import { titleField, descriptionField, ownerField, dateTimeFr } from '../../core/fields'

// Habilitation des changements (ITIL 4) : maximiser le nombre de changements
// réussis en évaluant les risques, en autorisant et en gérant le calendrier.
export const changeStatuses = [
  { value: 'Demandé', tone: 'new' }, { value: 'Évalué', tone: 'wait' },
  { value: 'Autorisé', tone: 'progress', manager: true }, { value: 'Rejeté', tone: 'bad', manager: true },
  { value: 'Planifié', tone: 'progress' }, { value: 'Mis en œuvre', tone: 'done' }, { value: 'Clos', tone: 'closed' },
]

export const changeConfig = {
  title: 'Changements',
  sub: 'Évaluer les risques, autoriser et planifier les changements pour qu\'ils réussissent (ITIL 4).',
  api: api.changes, basePath: '/changes', linkType: 'change',
  createLabel: '+ Demander un changement', createTitle: 'Demande de changement',
  empty: 'Aucun changement enregistré.', ownerLabel: 'Sous ma responsabilité',
  statuses: changeStatuses,
  fields: [
    titleField('Titre', 'Ex. : Migration du serveur de fichiers vers le nouveau stockage'),
    { key: 'changeType', label: 'Type', type: 'select', options: ['Standard', 'Normal', 'Urgent'], required: true, default: 'Normal' },
    { key: 'risk', label: 'Risque', type: 'select', options: ['Faible', 'Moyen', 'Élevé'], required: true, default: 'Moyen' },
    { key: 'plannedStart', label: 'Début planifié', type: 'datetime' },
    { key: 'plannedEnd', label: 'Fin planifiée', type: 'datetime' },
    ownerField('Responsable du changement'),
    descriptionField('Justification et périmètre'),
    { key: 'implementationPlan', label: 'Plan de mise en œuvre', type: 'textarea', rows: 3 },
    { key: 'backoutPlan', label: 'Plan de retour arrière', type: 'textarea', rows: 3 },
    { key: 'outcome', label: 'Résultat', type: 'select', options: ['Réussi', 'Échoué'], create: false },
  ],
  columns: [
    { h: 'Type', v: r => r.changeType },
    { h: 'Risque', v: r => r.risk },
    { h: 'Début planifié', v: r => dateTimeFr(r.plannedStart) },
  ],
  meta: r => [r.authorizedAt && `Autorisé par ${r.authorizedBy} le ${dateTimeFr(r.authorizedAt)}`],
  help: 'Standard : pré-autorisé. Normal et urgent : autorisés par un gestionnaire avant planification. Le résultat est requis pour clore.',
  linkHint: 'Problème ou demande à l\'origine, CI modifiés…',
}
