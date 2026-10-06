import React, { useEffect, useState } from 'react'
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
    ...ci.incoming.map(r => ({ id: r.id, text: `${r.type.toLowerCase()} ${ci.title}`, other: r.source, before: true })),
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

/** Une colonne de la vue d'impact : CI atteints, en retrait selon leur distance. */
function ImpactList({ title, hint, nodes }) {
  return (
    <div>
      <h4 style={{ color: 'var(--navy)', marginBottom: 2 }}>{title} ({nodes.length})</h4>
      <p style={{ color: 'var(--muted)', fontSize: 'var(--fs-sm)', marginBottom: 8 }}>{hint}</p>
      {nodes.length === 0 && <p style={{ color: 'var(--muted)', fontSize: 'var(--fs-sm)' }}>Aucun.</p>}
      {nodes.map(n => (
        <div key={n.id} style={{ paddingLeft: (n.depth - 1) * 18, marginBottom: 6 }}>
          <Link to={'/configuration/' + n.id}><b>{n.reference}</b> {n.title}</Link>
          <span style={{ color: 'var(--muted)', fontSize: 'var(--fs-xs)' }}> — {n.ciType}, {n.status} · {n.relation.toLowerCase()} {n.via.reference}</span>
        </div>
      ))}
    </div>
  )
}

/** Vue d'impact transitive : ce qui tombe si ce CI tombe (aval), ce dont il dépend (amont). */
function Impact({ ci }) {
  const [impact, setImpact] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.configurationItems.impact(ci.id).then(setImpact).catch(e => setError(e.message)) }, [ci])
  return (
    <div className="card" style={{ marginTop: 16 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Impact</h3>
      {error && <div className="error">{error}</div>}
      {impact && (
        <div className="row2">
          <ImpactList title="En aval" hint="Touchés si ce CI tombe." nodes={impact.downstream} />
          <ImpactList title="En amont" hint="Ce dont ce CI dépend." nodes={impact.upstream} />
        </div>
      )}
    </div>
  )
}

export default function ConfigurationItemDetail() {
  return (
    <RecordDetail config={ciConfig}>
      {(r, reload) => <><Relations ci={r} onChanged={reload} /><Impact ci={r} /></>}
    </RecordDetail>
  )
}
