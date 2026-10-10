import React from 'react'
import { api } from '../../api'
import { titleField, descriptionField, ownerField, dateTimeFr } from '../../core/fields'

// Gestion des incidents (ITIL 4) : minimiser l'impact négatif des incidents
// en rétablissant le service normal au plus vite.
export const incidentConfig = {
  title: 'Incidents',
  sub: 'Rétablir le service normal au plus vite et limiter l\'impact des interruptions (ITIL 4).',
  api: api.incidents, basePath: '/incidents', linkType: 'incident',
  createLabel: '+ Déclarer un incident', createTitle: 'Déclarer un incident',
  empty: 'Aucun incident enregistré.', ownerLabel: 'Assignés à moi',
  statuses: [
    { value: 'Nouveau', tone: 'new' }, { value: 'En cours', tone: 'progress' },
    { value: 'En attente', tone: 'wait' }, { value: 'Résolu', tone: 'done' }, { value: 'Clos', tone: 'closed' },
  ],
  fields: [
    titleField('Titre', 'Ex. : Messagerie inaccessible depuis le site de Tana'),
    descriptionField('Description', 'Symptômes, utilisateurs touchés, depuis quand…'),
    { key: 'impact', label: 'Impact', type: 'select', options: ['Faible', 'Moyen', 'Élevé'], required: true, default: 'Moyen' },
    { key: 'urgency', label: 'Urgence', type: 'select', options: ['Faible', 'Moyenne', 'Élevée'], required: true, default: 'Moyenne' },
    { key: 'category', label: 'Catégorie', placeholder: 'Réseau, Poste de travail, Application…' },
    { key: 'affectedService', label: 'Service affecté', placeholder: 'Ex. : Messagerie' },
    ownerField('Assigné à'),
    { key: 'isMajor', label: 'Gravité', type: 'checkbox', checkboxLabel: 'Incident majeur' },
    { key: 'resolution', label: 'Résolution', type: 'textarea', create: false, rows: 3,
      placeholder: 'Obligatoire pour passer à Résolu ou Clos.' },
    { key: 'resolutionCode', label: 'Code de résolution', type: 'select', create: false,
      options: ['Correctif appliqué', 'Contournement', 'Résolu sans action', 'Non reproductible', 'Doublon'] },
  ],
  columns: [
    { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> },
    { h: 'Service', v: r => r.affectedService || '—' },
    { h: 'Assigné à', v: r => r.ownerDisplayName || '—' },
  ],
  badges: r => <>
    <span className={'badge ' + r.priority}>{r.priority}</span>{' '}
    {r.isMajor && <span className="badge t-bad">Majeur</span>}
  </>,
  meta: r => [r.firstResponseAt && `Pris en charge le ${dateTimeFr(r.firstResponseAt)}`,
    r.resolvedAt && `Résolu le ${dateTimeFr(r.resolvedAt)}`],
  help: 'Prise en charge (En cours) : un responsable. Résolution : description et code ; un doublon est relié à l\'incident conservé (onglet Liens).',
  linkHint: 'Problème sous-jacent, CI touchés, article de connaissance appliqué…',
}
