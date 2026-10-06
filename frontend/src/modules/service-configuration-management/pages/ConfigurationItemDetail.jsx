import React, { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { ciConfig, RELATION_TYPES } from '../config'

/** Dépendances du CI : relations sortantes (ce CI → autre) et entrantes (autre → ce CI). */
function Relations({ ci, onChanged }) {
  const [type, setType] = useState(RELATION_TYPES[0])
  const [reference, setReference] = useState('')
  const [error, setError] = useState('')
  const add = () => api.configurationItems.addRelation(ci.id, { targetReference: reference, type })
    .then(() => { setReference(''); setError(''); onChanged() }).catch(e => setError(e.message))
  const remove = id => api.configurationItems.removeRelation(id).then(onChanged).catch(e => setError(e.message))

  const rows = [
    ...ci.outgoing.map(r => ({ id: r.id, text: `${ci.title} ${r.type.toLowerCase()}`, other: r.target })),
    ...ci.incoming.map(r => ({ id: r.id, text: `${r.type} ${ci.title}`, other: r.source, before: true })),
  ]
  return (
    <div className="card" style={{ marginTop: 16 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Relations ({rows.length})</h3>
      {error && <div className="error">{error}</div>}
      {rows.length > 0 && (
        <table style={{ marginBottom: 12 }}>
          <tbody>{rows.map(r => {
            const other = <Link to={'/configuration/' + r.other.id}><b>{r.other.reference}</b> {r.other.title}</Link>
            return (
              <tr key={r.id}>
                <td>{r.before ? <>{other} {r.text}</> : <>{r.text} {other}</>}</td>
                <td style={{ textAlign: 'right' }}>
                  <button className="btn ghost small" onClick={() => remove(r.id)} title="Retirer la relation">×</button>
                </td>
              </tr>)
          })}</tbody>
        </table>
      )}
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
        <select style={{ maxWidth: 180 }} value={type} onChange={e => setType(e.target.value)}>
          {RELATION_TYPES.map(t => <option key={t}>{t}</option>)}
        </select>
        <input style={{ maxWidth: 220 }} value={reference} onChange={e => setReference(e.target.value)}
          placeholder="CI cible, ex. CI-2026-0002" />
        <button className="btn ghost" onClick={add} disabled={!reference}>Ajouter</button>
      </div>
    </div>
  )
}

export default function ConfigurationItemDetail() {
  return <RecordDetail config={ciConfig}>{(r, reload) => <Relations ci={r} onChanged={reload} />}</RecordDetail>
}
