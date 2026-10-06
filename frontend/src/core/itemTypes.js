// Types d'enregistrements reliables entre processus : libellé et page de
// détail. Miroir de ItemLinks.Kinds côté API (backend/Core/Links/ItemLink.cs).
export const ITEM_TYPES = {
  incident: { label: 'Incident', path: '/incidents' },
  request: { label: 'Demande', path: '/requests' },
  problem: { label: 'Problème', path: '/problems' },
  change: { label: 'Changement', path: '/changes' },
  ci: { label: 'CI', path: '/configuration' },
  service: { label: 'Service', path: '/services' },
  agreement: { label: 'SLA', path: '/agreements' },
  article: { label: 'Article', path: '/knowledge' },
  improvement: { label: 'Amélioration', path: '/improvements' },
}

export const hrefOf = (type, id) => `${ITEM_TYPES[type]?.path ?? ''}/${id}`
