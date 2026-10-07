import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api'
import { ITEM_TYPES, hrefOf } from '../itemTypes'

/**
 * Enregistrements reliés à (type, id), tous processus confondus, et ajout
 * d'un lien par référence (INC-…, PRB-…, CHG-…). `hint` suggère les liens
 * attendus par le processus.
 */
export default function LinkedItems({ type, id, hint, refreshKey }) {
  const [items, setItems] = useState([])
  const [reference, setReference] = useState('')
  const [error, setError] = useState('')

  const load = () => api.links.list(type, id).then(setItems).catch(e => setError(e.message))
  useEffect(() => { load() }, [type, id, refreshKey])

  const add = () => api.links.create({ fromType: type, fromId: Number(id), toReference: reference })
    .then(() => { setReference(''); setError(''); load() })
    .catch(e => setError(e.message))
  const remove = (linkId) => api.links.remove(linkId).then(load).catch(e => setError(e.message))

  return (
    <div className="card">
      <h3 style={{ color: 'var(--navy)', marginBottom: 4 }}>Éléments liés ({items.length})</h3>
      {hint && <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 10 }}>{hint}</p>}
      {error && <div className="error">{error}</div>}
      {items.length === 0 && <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 12 }}>Aucun élément lié pour l'instant.</p>}
      {items.length > 0 && (
        <table style={{ marginBottom: 12 }}>
          <thead><tr><th>Type</th><th>Référence</th><th>Titre</th><th>Statut</th><th /></tr></thead>
          <tbody>{items.map(l => (
            <tr key={l.linkId}>
              <td>{ITEM_TYPES[l.type]?.label ?? l.type}</td>
              <td><Link to={hrefOf(l.type, l.id)}><b>{l.reference}</b></Link></td>
              <td>{l.title}</td>
              <td>{l.status}</td>
              <td style={{ textAlign: 'right' }}>
                <button className="btn ghost small" onClick={() => remove(l.linkId)} title="Retirer le lien">×</button>
              </td>
            </tr>))}
          </tbody>
        </table>
      )}
      <div style={{ display: 'flex', gap: 10 }}>
        <input style={{ maxWidth: 260 }} value={reference} onChange={e => setReference(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && reference && add()} placeholder="Référence, ex. PRB-2026-0001" />
        <button className="btn ghost" onClick={add} disabled={!reference}>Lier</button>
      </div>
    </div>
  )
}
