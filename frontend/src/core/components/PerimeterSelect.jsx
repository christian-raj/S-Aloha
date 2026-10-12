import React from 'react'

/**
 * Choix du périmètre (PER-04 à la création, PER-07 au déplacement). Masqué quand
 * l'utilisateur n'a qu'un périmètre : l'API le retient d'office.
 */
export default function PerimeterSelect({ perimeters, value, onChange, disabled, required }) {
  if (perimeters.length < 2) return null
  return (
    <div className="field" style={{ maxWidth: 320 }}>
      <label>Périmètre{required && ' *'}</label>
      <select value={value ?? ''} onChange={e => onChange(e.target.value ? Number(e.target.value) : null)} disabled={disabled}>
        {required && <option value="">— Choisir —</option>}
        {perimeters.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select>
    </div>
  )
}
