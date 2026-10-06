import React, { useState } from 'react'
import DirectoryPicker from './DirectoryPicker'

const ROLES = [
  { code: 'R', label: 'Responsable (réalise)' },
  { code: 'A', label: 'Approbateur (rend compte)' },
  { code: 'C', label: 'Consulté' },
  { code: 'I', label: 'Informé' }
]

/** Sélecteur d'utilisateurs / groupes AD avec rôle RACI. */
export default function RaciEditor({ value, onChange }) {
  const [role, setRole] = useState('R')

  const add = (entry) => {
    if (!value.some(r => r.role === role && r.assigneeId === entry.id))
      onChange([...value, {
        role, assigneeType: entry.type, assigneeId: entry.id, assigneeDisplayName: entry.displayName
      }])
  }

  const remove = (i) => onChange(value.filter((_, idx) => idx !== i))

  return (
    <div>
      <div className="row2" style={{ alignItems: 'end' }}>
        <div className="field">
          <label>Rôle RACI</label>
          <select value={role} onChange={e => setRole(e.target.value)}>
            {ROLES.map(r => <option key={r.code} value={r.code}>{r.code} — {r.label}</option>)}
          </select>
        </div>
        <div className="field">
          <label>Utilisateur ou groupe AD</label>
          <DirectoryPicker onPick={add} />
        </div>
      </div>
      <div>
        {value.map((r, i) => (
          <span className="chip" key={i}>
            <span className={'badge ' + r.role}>{r.role}</span>
            {r.assigneeType === 'Group' ? '👥' : '👤'} {r.assigneeDisplayName}
            <button onClick={() => remove(i)} title="Retirer">×</button>
          </span>
        ))}
        {value.length === 0 && <p style={{ color: 'var(--muted)', fontSize: 13 }}>
          Ajoutez au moins un Responsable (R) et exactement un Approbateur (A).</p>}
      </div>
    </div>
  )
}
