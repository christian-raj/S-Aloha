import React, { useState } from 'react'
import { api } from '../../api'

/** Recherche en direct dans l'annuaire AD ; `onPick` reçoit { type, id, displayName }. */
export default function DirectoryPicker({ onPick, placeholder = 'Tapez au moins 2 caractères…', disabled }) {
  const [q, setQ] = useState('')
  const [results, setResults] = useState([])

  const search = async (text) => {
    setQ(text)
    if (text.length < 2) { setResults([]); return }
    try { setResults(await api.directory.search(text)) } catch { setResults([]) }
  }

  const pick = (entry) => { onPick(entry); setQ(''); setResults([]) }

  return (
    <div className="raci-pick">
      <input value={q} onChange={e => search(e.target.value)} placeholder={placeholder} disabled={disabled} />
      {results.length > 0 && (
        <div className="raci-results">
          {results.map(r => (
            <div key={r.type + r.id} onClick={() => pick(r)}>
              {r.type === 'Group' ? '👥 ' : '👤 '}{r.displayName} <span style={{ color: 'var(--muted)' }}>({r.id})</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
