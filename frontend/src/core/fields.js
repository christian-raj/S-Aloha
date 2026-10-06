// Champs communs à tous les processus (socle Record de l'API).
export const titleField = (label = 'Titre', placeholder) =>
  ({ key: 'title', label, required: true, full: true, placeholder })

export const descriptionField = (label = 'Description', placeholder, rows = 4) =>
  ({ key: 'description', label, type: 'textarea', placeholder, rows })

/** Responsable AD (assigné, propriétaire, porteur…) : utilisateur ou groupe. */
export const ownerField = (label) =>
  ({ key: 'ownerId', nameKey: 'ownerDisplayName', typeKey: 'ownerType', label, type: 'person' })

export const dateFr = d => (d ? new Date(d).toLocaleDateString('fr-FR') : '—')
export const dateTimeFr = d => (d ? new Date(d).toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' }) : '—')
