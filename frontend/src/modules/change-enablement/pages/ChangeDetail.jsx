import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, getUser } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { dateTimeFr } from '../../../core/fields'
import usePerimeters from '../../../core/usePerimeters'
import useChangeConfig from '../useChangeConfig'

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

/**
 * CHG-20 : CAB d'un changement Normal à risque Élevé — décisions des gestionnaires ; deux
 * approbations distinctes l'autorisent, un rejet motivé le rejette. Le demandeur ne siège pas.
 */
export function CabCard({ change, onDecided }) {
  const [approvals, setApprovals] = useState([])
  const [comment, setComment] = useState('')
  const [error, setError] = useState('')
  const perimeters = usePerimeters()
  useEffect(() => { api.changes.approvals(change.id).then(setApprovals).catch(() => setApprovals([])) }, [change.id, change.updatedAt])
  if (!change.requiresCab && approvals.length === 0) return null
  const user = getUser()
  const here = perimeters.find(p => p.id === change.perimeterId)?.role
  const isManager = ['Admin', 'Manager'].includes(here ?? user?.role)
  const mine = approvals.some(a => a.approver?.toLowerCase() === user?.username?.toLowerCase())
  const canDecide = change.requiresCab && change.status === 'Évalué' && isManager && !mine
    && change.createdBy?.toLowerCase() !== user?.username?.toLowerCase()
  const decide = approve => api.changes.decide(change.id, { approve, comment })
    .then(() => { setComment(''); setError(''); onDecided() }).catch(e => setError(e.message))
  return (
    <div className="card" style={{ marginTop: 16 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 4 }}>Comité consultatif des changements (CAB)</h3>
      <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 8 }}>
        Risque élevé : deux gestionnaires distincts approuvent ; un seul rejet, motivé, suffit à rejeter. Le demandeur ne siège pas.</p>
      {approvals.length === 0 && <p className="muted small">Aucune décision pour l'instant.</p>}
      {approvals.map(a => (
        <p key={a.id} style={{ fontSize: 13, margin: '4px 0' }}>
          <span className={'badge ' + (a.approved ? 't-done' : 't-bad')}>{a.approved ? 'Approuvé' : 'Rejeté'}</span>{' '}
          <b>{a.approverDisplayName}</b> le {dateTimeFr(a.at)}{a.comment && <> — {a.comment}</>}</p>))}
      {canDecide && (
        <div style={{ marginTop: 8 }}>
          {error && <div className="error">{error}</div>}
          <div className="field"><label htmlFor="cab-comment">Commentaire (obligatoire pour rejeter)</label>
            <textarea id="cab-comment" rows={2} value={comment} onChange={e => setComment(e.target.value)} /></div>
          <button className="btn" onClick={() => decide(true)}>Approuver</button>{' '}
          <button className="btn ghost" onClick={() => decide(false)} disabled={!comment.trim()}>Rejeter</button>
        </div>)}
    </div>
  )
}

export default function ChangeDetail() {
  const config = useChangeConfig()
  if (!config) return <p>Chargement…</p>
  return <RecordDetail config={config} tab={[
    { label: 'Conflits', render: r => <Conflicts change={r} /> },
    { label: 'Impact', render: r => <ChangeImpact change={r} /> },
  ]}>{(r, reload) => <CabCard change={r} onDecided={reload} />}</RecordDetail>
}
