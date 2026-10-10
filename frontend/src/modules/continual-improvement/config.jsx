import { api } from '../../api'
import { titleField, descriptionField, ownerField, dateFr, dateTimeFr } from '../../core/fields'

/** Modèle d'amélioration continue ITIL 4, étapes 1 à 7. */
export const STEPS = [
  'Quelle est la vision ?', 'Où en sommes-nous ?', 'Où voulons-nous être ?', 'Comment y parvenir ?',
  'Passer à l\'action', 'Y sommes-nous parvenus ?', 'Comment maintenir la dynamique ?',
]

// Amélioration continue (ITIL 4) : aligner les services sur l'évolution des
// besoins par l'amélioration continue des produits, services et pratiques.
export const improvementConfig = {
  title: 'Amélioration continue',
  sub: 'Registre d\'amélioration continue : opportunités, valeur attendue, mesures et avancement selon le modèle ITIL 4 en 7 étapes.',
  api: api.improvements, basePath: '/improvements', linkType: 'improvement',
  createLabel: '+ Proposer une amélioration', createTitle: 'Proposer une amélioration',
  empty: 'Aucune amélioration au registre.', ownerLabel: 'Que je porte',
  statuses: [
    { value: 'Proposée', tone: 'new' }, { value: 'Validée', tone: 'progress', manager: true },
    { value: 'En cours', tone: 'progress' }, { value: 'Réalisée', tone: 'done' },
    { value: 'Abandonnée', tone: 'closed', manager: true },
  ],
  fields: [
    titleField('Titre', 'Ex. : Réduire le délai de traitement des demandes d\'accès'),
    { key: 'priority', label: 'Priorité', type: 'select', options: ['Faible', 'Moyenne', 'Élevée'], required: true, default: 'Moyenne' },
    { key: 'step', label: 'Étape du modèle', type: 'select', required: true, numeric: true, default: 1,
      options: STEPS.map((s, i) => ({ value: i + 1, label: `${i + 1}. ${s}` })) },
    ownerField('Porteur'),
    { key: 'dueDate', label: 'Échéance', type: 'date' },
    descriptionField('Opportunité', 'Constat, irritant, besoin métier…', 3),
    { key: 'benefit', label: 'Valeur attendue', type: 'textarea', rows: 2 },
    { key: 'baseline', label: 'Mesure de départ', placeholder: 'Ex. : 5 jours en moyenne' },
    { key: 'target', label: 'Mesure cible', placeholder: 'Ex. : 2 jours' },
    { key: 'outcome', label: 'Résultat constaté', type: 'textarea', rows: 2, create: false,
      placeholder: 'Obligatoire pour déclarer l\'amélioration réalisée.' },
    { key: 'abandonReason', label: 'Motif d\'abandon', type: 'textarea', rows: 2, create: false,
      placeholder: 'Obligatoire pour abandonner.' },
  ],
  columns: [
    { h: 'Étape', v: r => `${r.step}. ${STEPS[r.step - 1]}` },
    { h: 'Priorité', v: r => r.priority },
    { h: 'Porteur', v: r => r.ownerDisplayName || '—' },
    { h: 'Échéance', v: r => dateFr(r.dueDate) },
  ],
  meta: r => [r.validatedAt && `Validée par ${r.validatedBy} le ${dateTimeFr(r.validatedAt)}`],
  help: 'Validation par un gestionnaire, avec valeur attendue, mesure de départ et mesure cible. Réalisée : à l\'étape 6 au moins, avec le résultat constaté. Un abandon se motive.',
  linkHint: 'Problèmes, incidents récurrents, services ou SLA à l\'origine de l\'amélioration…',
}
