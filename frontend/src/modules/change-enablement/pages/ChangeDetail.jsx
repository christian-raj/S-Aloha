import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { changeConfig } from '../config'

/** Conflits de calendrier : autres changements sur le même CI, créneaux qui se chevauchent. */
function Conflicts({ change }) {
  const [conflicts, setConflicts] = useState([])
  useEffect(() => { api.changes.conflicts(change.id).then(setConflicts).catch(() => setConflicts([])) }, [change])
  if (conflicts.length === 0)
    return <div className="card"><p style={{ color: 'var(--muted)', fontSize: 13 }}>Aucun conflit de calendrier : aucun autre changement sur les mêmes CI pendant ce créneau.</p></div>
  return (
    <div className="error">
      <b>Conflit de calendrier</b> — ce changement chevauche :
      <ul style={{ margin: '6px 0 0 18px' }}>
        {conflicts.map(c => (
          <li key={c.changeId + '-' + c.ciId}>
            <Link to={'/changes/' + c.changeId}><b>{c.changeReference}</b> {c.changeTitle}</Link>
            {' '}sur <Link to={'/configuration/' + c.ciId}>{c.ciReference} {c.ciTitle}</Link>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** CFG-20 : ce que le changement peut toucher — ses CI, ce qui en dépend en aval, les services. */
export function ChangeImpact({ change }) {
  const [impact, setImpact] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.configurationItems.impactOf('change', change.id).then(setImpact).catch(e => setError(e.message)) }, [change])
  if (error) return <div className="error">{error}</div>
  if (!impact) return <p>Chargement…</p>
  if (impact.cis.length === 0)
    return <div className="card"><p style={{ color: 'var(--muted)', fontSize: 13 }}>Aucun CI relié : reliez les CI touchés (onglet Liens) pour voir l'impact du changement.</p></div>
  const ci = n => n.id ? <Link to={'/configuration/' + n.id}><b>{n.reference}</b> {n.title}</Link> : <span className="muted">{n.title}</span>
  return (
    <div className="card">
      <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Impact du changement</h3>
      <div className="row2">
        <div>
          <h4 style={{ color: 'var(--navy)', marginBottom: 6 }}>CI modifiés ({impact.cis.length})</h4>
          {impact.cis.map(c => <div key={c.id} style={{ marginBottom: 6 }}>{ci(c)} <span className="muted">— {c.ciType}, {c.status}</span></div>)}
        </div>
        <div>
          <h4 style={{ color: 'var(--navy)', marginBottom: 6 }}>Touchés en aval ({impact.downstream.length})</h4>
          {impact.downstream.length === 0 && <p className="muted">Aucun.</p>}
          {impact.downstream.map((n, i) => (
            <div key={n.id || 'x' + i} style={{ paddingLeft: (n.depth - 1) * 18, marginBottom: 6 }}>
              {ci(n)} <span className="muted" style={{ fontSize: 'var(--fs-xs)' }}>— {n.relation.toLowerCase()} {n.via.reference}</span>
            </div>))}
        </div>
      </div>
      <h4 style={{ color: 'var(--navy)', margin: '12px 0 6px' }}>Services touchés ({impact.services.length})</h4>
      {impact.services.length === 0 && <p className="muted">Aucun service du catalogue relié à ces CI.</p>}
      {impact.services.map(s => (
        <div key={s.id} style={{ marginBottom: 6 }}>
          <Link to={'/services/' + s.id}><b>{s.reference}</b> {s.title}</Link>
          <span className="muted" style={{ fontSize: 'var(--fs-xs)' }}> — {s.status}{s.ownerDisplayName && ` · ${s.ownerDisplayName}`}</span>
        </div>))}
    </div>
  )
}

export default function ChangeDetail() {
  return <RecordDetail config={changeConfig} tab={[
    { label: 'Conflits', render: r => <Conflicts change={r} /> },
    { label: 'Impact', render: r => <ChangeImpact change={r} /> },
  ]} />
}
