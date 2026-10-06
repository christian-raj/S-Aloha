import React from 'react'

/** Badge de statut coloré selon la tonalité déclarée dans la configuration du module. */
export default function StatusBadge({ statuses, status }) {
  const tone = statuses.find(s => s.value === status)?.tone ?? 'closed'
  return <span className={'badge t-' + tone}>{status}</span>
}
