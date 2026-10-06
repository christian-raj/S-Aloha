import { api } from '../../api'
import { titleField, descriptionField, ownerField } from '../../core/fields'

export const CI_TYPES = ['Application', 'Serveur', 'Base de données', 'Réseau', 'Stockage', 'Poste de travail', 'Logiciel', 'Autre']
export const RELATION_TYPES = ['Dépend de', 'Héberge', 'Fait partie de', 'Se connecte à']

// Gestion de la configuration des services (ITIL 4) : une information exacte
// et fiable sur les services et les CI, quand et où elle est nécessaire.
export const ciConfig = {
  title: 'Configuration',
  sub: 'Éléments de configuration (CI) et leurs dépendances : une information fiable, disponible quand il le faut (ITIL 4).',
  api: api.configurationItems, basePath: '/configuration', linkType: 'ci',
  createLabel: '+ Nouveau CI', createTitle: 'Nouvel élément de configuration',
  empty: 'Aucun élément de configuration enregistré.', ownerLabel: 'Dont je suis propriétaire',
  titleLabel: 'Nom', statusAtCreation: 'En service (par défaut)',
  filter: { key: 'type', label: 'Tous les types', options: CI_TYPES },
  statuses: [
    { value: 'Planifié', tone: 'new' }, { value: 'En service', tone: 'done' },
    { value: 'Hors service', tone: 'wait' }, { value: 'Retiré', tone: 'closed' },
  ],
  fields: [
    titleField('Nom', 'Ex. : srv-erp-01'),
    { key: 'ciType', label: 'Type', type: 'select', options: CI_TYPES, required: true, default: 'Serveur' },
    { key: 'environment', label: 'Environnement', type: 'select', options: ['Production', 'Recette', 'Développement', 'Autre'], required: true, default: 'Production' },
    { key: 'location', label: 'Emplacement', placeholder: 'Salle serveur, cloud, site…' },
    ownerField('Propriétaire'),
    descriptionField('Description', undefined, 3),
  ],
  columns: [
    { h: 'Type', v: r => r.ciType },
    { h: 'Environnement', v: r => r.environment },
    { h: 'Propriétaire', v: r => r.ownerDisplayName || '—' },
  ],
  linkHint: 'Services rendus, incidents et changements qui touchent ce CI…',
}
