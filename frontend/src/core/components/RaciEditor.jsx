import React, { useState } from 'react'
import { api } from '../../api'

const ROLES = [
  { code: 'R', label: 'Responsable (réalise)' },
  { code: 'A', label: 'Approbateur (rend compte)' },
  { code: 'C', label: 'Consulté' },
  { code: 'I', label: 'Informé' }
]

/** Sélecteur d'utilisateurs / groupes AD avec rôle RACI. */
export default function RaciEditor({ value, onChange }) {
  const [role, setRole] = useState('R')
  const [q, setQ] = useState('')
  const [results, setResults] = useState([])

  const search = async (text) => {
    setQ(text)
    if (text.length < 2) { setResults([]); return }
    try { setResults(await api.directory.search(text)) } catch { setResults([]) }
  }

  const add = (entry) => {
    if (!value.some(r => r.role === role && r.assigneeId === entry.id))
      onChange([...value, {
        role, assigneeType: entry.type, assigneeId: entry.id, assigneeDisplayName: entry.displayName
      }])
    setQ(''); setResults([])
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
        <div className="field raci-pick">
          <label>Utilisateur ou groupe AD</label>
          <input value={q} onChange={e => search(e.target.value)}
            placeholder="Tapez au moins 2 caractères…" />
          {results.length > 0 && (
            <div className="raci-results">
              {results.map(r => (
                <div key={r.type + r.id} onClick={() => add(r)}>
                  {r.type === 'Group' ? '👥 ' : '👤 '}{r.displayName} <span style={{ color: 'var(--muted)' }}>({r.id})</span>
                </div>
              ))}
            </div>
          )}
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
