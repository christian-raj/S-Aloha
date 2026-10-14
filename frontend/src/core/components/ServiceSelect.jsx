import React from 'react'
import { serviceLabel } from '../useServices'

/**
 * SLM-10 : choix du service dans le catalogue. Un libellé antérieur inconnu du catalogue
 * (`legacy`) reste affiché tant qu'aucun service n'est choisi.
 */
export default function ServiceSelect({ services, value, onChange, disabled, legacy }) {
  return (
    <select value={value ?? ''} onChange={onChange} disabled={disabled} aria-label="Service affecté">
      <option value="">{legacy ? `${legacy} (hors catalogue)` : '—'}</option>
      {services.map(s => <option key={s.id} value={s.id}>{serviceLabel(s)}</option>)}
    </select>
  )
}
