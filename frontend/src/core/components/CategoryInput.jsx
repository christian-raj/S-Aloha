import React from 'react'
import { activeNames } from '../useCategories'

/**
 * ADM-51 : catégorie choisie dans la liste de la pratique dès qu'elle en a une ; une valeur
 * déjà portée hors liste reste affichée. Sans liste (ou pendant le chargement), saisie libre.
 */
export default function CategoryInput({ categories, value, onChange, disabled, placeholder }) {
  const names = activeNames(categories)
  if (names.length === 0)
    return <input value={value ?? ''} onChange={onChange} disabled={disabled} placeholder={placeholder} aria-label="Catégorie" />
  return (
    <select value={value ?? ''} onChange={onChange} disabled={disabled} aria-label="Catégorie">
      <option value="">—</option>
      {value && !names.includes(value) && <option value={value}>{value} (hors liste)</option>}
      {names.map(n => <option key={n}>{n}</option>)}
    </select>
  )
}
