import { api } from '../../api'
import { titleField, descriptionField, ownerField, dateFr, dateTimeFr } from '../../core/fields'

// Gestion des demandes de service (ITIL 4) : traiter les demandes prédéfinies
// initiées par les utilisateurs, de façon efficace et conviviale.
export const requestConfig = {
  title: 'Demandes',
  sub: 'Traiter les demandes de service des utilisateurs (accès, matériel, information) selon la qualité convenue (ITIL 4).',
  api: api.requests, basePath: '/requests', linkType: 'request',
  createLabel: '+ Nouvelle demande', createTitle: 'Nouvelle demande de service',
  empty: 'Aucune demande enregistrée.', ownerLabel: 'Traitées par moi',
  statuses: [
    { value: 'Soumise', tone: 'new' }, { value: 'Approuvée', tone: 'progress', manager: true },
    { value: 'Rejetée', tone: 'bad', manager: true }, { value: 'En cours', tone: 'progress' },
    { value: 'Satisfaite', tone: 'done' }, { value: 'Close', tone: 'closed' },
  ],
  fields: [
    titleField('Titre', 'Ex. : Accès VPN pour un nouvel arrivant'),
    { key: 'requestedItem', label: 'Objet demandé', required: true, placeholder: 'Accès VPN, poste portable, licence…' },
    { key: 'requestedFor', nameKey: 'requestedForDisplayName', label: 'Bénéficiaire', type: 'person' },
    { key: 'dueDate', label: 'Échéance souhaitée', type: 'date' },
    ownerField('Traitée par'),
    descriptionField('Précisions'),
  ],
  columns: [
    { h: 'Objet demandé', v: r => r.requestedItem },
    { h: 'Bénéficiaire', v: r => r.requestedForDisplayName || '—' },
    { h: 'Échéance', v: r => dateFr(r.dueDate) },
  ],
  meta: r => [r.approvedAt && `Approuvée par ${r.approvedBy} le ${dateTimeFr(r.approvedAt)}`],
  help: 'Une demande est approuvée (ou rejetée) par un gestionnaire avant d\'être traitée.',
  linkHint: 'Service du catalogue concerné, changement standard déclenché…',
}
