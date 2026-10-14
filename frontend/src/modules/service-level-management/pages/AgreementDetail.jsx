import React, { useState } from 'react'
import { api, getUser } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { dateFr, dateTimeFr } from '../../../core/fields'
import usePerimeters from '../../../core/usePerimeters'
import useAgreementConfig from './useAgreementConfig'

/**
 * SLM-22 : revues du SLA — le gestionnaire enregistre constats et décisions ; la
 * prochaine revue est reportée de 12 mois, sauf date choisie.
 */
export function AgreementReviews({ agreement, onSaved }) {
  const perimeters = usePerimeters()
  const here = perimeters.find(p => p.id === agreement.perimeterId)?.role
  const isManager = ['Admin', 'Manager'].includes(here ?? getUser()?.role)
  const [f, setF] = useState({ findings: '', decisions: '', nextReviewDate: '' })
  const [error, setError] = useState('')
  const reviews = agreement.reviews ?? []
  const save = () => api.agreements.review(agreement.id, { ...f, nextReviewDate: f.nextReviewDate || null })
    .then(() => { setF({ findings: '', decisions: '', nextReviewDate: '' }); setError(''); onSaved() })
    .catch(e => setError(e.message))

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 4 }}>Revues du SLA</h3>
      <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 10 }}>
        Prochaine revue : {dateFr(agreement.reviewDate)}. Une revue enregistrée la reporte de 12 mois, sauf date choisie.</p>
      {reviews.length === 0 && <p className="muted small">Aucune revue enregistrée.</p>}
      {reviews.map(r => (
        <div key={r.id} style={{ borderTop: '1px solid var(--line)', padding: '8px 0', fontSize: 13 }}>
          <b>{dateTimeFr(r.reviewedAt)}</b> — {r.reviewedBy} · prochaine le {dateFr(r.nextReviewDate)}
          {r.findings && <p style={{ margin: '4px 0', whiteSpace: 'pre-line' }}><b>Constats :</b> {r.findings}</p>}
          {r.decisions && <p style={{ margin: '4px 0', whiteSpace: 'pre-line' }}><b>Décisions :</b> {r.decisions}</p>}
        </div>))}
      {isManager && agreement.status !== 'Expiré' && (
        <div style={{ borderTop: '1px solid var(--line)', paddingTop: 10 }}>
          {error && <div className="error">{error}</div>}
          <div className="row2">
            <div className="field"><label htmlFor="review-findings">Constats</label>
              <textarea id="review-findings" rows={2} value={f.findings} onChange={e => setF({ ...f, findings: e.target.value })}
                placeholder="Respect des délais, incidents marquants, retours du client…" /></div>
            <div className="field"><label htmlFor="review-decisions">Décisions</label>
              <textarea id="review-decisions" rows={2} value={f.decisions} onChange={e => setF({ ...f, decisions: e.target.value })}
                placeholder="Cibles revues, actions, amélioration…" /></div>
          </div>
          <div className="field" style={{ maxWidth: 220 }}><label htmlFor="review-next">Prochaine revue (facultatif)</label>
            <input id="review-next" type="date" value={f.nextReviewDate} onChange={e => setF({ ...f, nextReviewDate: e.target.value })} /></div>
          <button className="btn" onClick={save} disabled={!f.findings.trim() && !f.decisions.trim()}>Enregistrer la revue</button>
        </div>)}
    </div>
  )
}

export default function AgreementDetail() {
  const config = useAgreementConfig()
  return config
    ? <RecordDetail config={config}>{(r, reload) => <AgreementReviews agreement={r} onSaved={reload} />}</RecordDetail>
    : <p>Chargement…</p>
}
