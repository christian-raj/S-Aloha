import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import { dateFr } from '../../../core/fields'

const pct = p => (p === null || p === undefined ? '—' : `${p.toLocaleString('fr-FR')} %`)
const delta = d => (d === null || d === undefined ? '—' : `${d > 0 ? '+' : ''}${d.toLocaleString('fr-FR')}`)
const tone = d => (d > 0 ? { color: 'var(--accent-emerald)' } : d < 0 ? { color: 'var(--danger)' } : {})

function ScoreRow({ label, score, sub }) {
  return (
    <tr>
      <td style={sub ? { paddingLeft: 24 } : {}}>{sub ? label : <b>{label}</b>}</td>
      <td>{pct(score.other)}</td><td>{pct(score.current)}</td>
      <td style={{ fontWeight: 600, ...tone(score.delta) }}>{delta(score.delta)}{score.delta != null && ' pt'}</td>
    </tr>
  )
}

/**
 * NIS-09 : comparaison avec une autre évaluation du même périmètre — évolution des
 * scores par pilier, objectif et thématique, et exigences dont la réponse a changé.
 */
export default function Comparison({ assessment }) {
  const [others, setOthers] = useState(null)
  const [otherId, setOtherId] = useState('')
  const [c, setC] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    api.assessments.list().then(list => {
      const same = list.filter(a => a.id !== assessment.id && a.perimeterId === assessment.perimeterId)
        .sort((a, b) => (b.validatedAt ?? b.createdAt).localeCompare(a.validatedAt ?? a.createdAt))
      setOthers(same)
      // Par défaut : la plus récente validée, sinon la plus récente.
      const first = same.find(a => a.status === 'Validée') ?? same[0]
      if (first) setOtherId(String(first.id))
    }).catch(e => setError(e.message))
  }, [assessment.id])
  useEffect(() => {
    if (!otherId) { setC(null); return }
    api.assessments.compare(assessment.id, otherId).then(setC).catch(e => setError(e.message))
  }, [assessment.id, otherId])

  if (error) return <div className="error">{error}</div>
  if (!others) return <p>Chargement…</p>
  if (others.length === 0) return <div className="card"><p className="muted">Aucune autre évaluation de ce périmètre à comparer.</p></div>

  return (
    <div className="card">
      <div className="field" style={{ maxWidth: 480 }}>
        <label htmlFor="compare-with">Comparer avec</label>
        <select id="compare-with" value={otherId} onChange={e => setOtherId(e.target.value)}>
          {others.map(a => <option key={a.id} value={a.id}>
            {a.reference} — {a.title} ({a.status}{a.validatedAt ? ` le ${dateFr(a.validatedAt)}` : ''})</option>)}
        </select>
      </div>
      {c && <>
        {c.current.referentialVersion !== c.other.referentialVersion &&
          <p className="muted small">Versions du référentiel différentes : {c.other.referentialVersion} → {c.current.referentialVersion}.</p>}
        <table>
          <thead><tr><th /><th>{c.other.reference}</th><th>{c.current.reference}</th><th>Évolution</th></tr></thead>
          <tbody>
            <ScoreRow label="Score global" score={c.global} />
            {c.pillars.map(p => <ScoreRow key={p.pillar} label={`Pilier ${p.pillar}`} score={p.score} />)}
            {c.objectives.map(o => (
              <React.Fragment key={o.id}>
                <ScoreRow label={`${o.id}. ${o.title}`} score={o.score} />
                {o.themes.length > 1 && o.themes.map(t => <ScoreRow key={t.theme} sub label={t.theme} score={t.score} />)}
              </React.Fragment>))}
          </tbody>
        </table>
        <h3 style={{ margin: '16px 0 6px' }}>Exigences dont la réponse a changé ({c.requirements.length})</h3>
        {c.requirements.length === 0
          ? <p className="muted small">Aucune.</p>
          : <table>
            <thead><tr><th>Exigence</th><th>Thématique</th><th>{c.other.reference}</th><th>{c.current.reference}</th><th>Évolution</th></tr></thead>
            <tbody>{c.requirements.map(r => (
              <tr key={r.code}><td><b>{r.code}</b></td><td>{r.theme}</td><td>{r.other}</td><td>{r.current}</td>
                <td style={{ fontWeight: 600, ...tone(r.delta) }}>{delta(r.delta)}</td></tr>))}</tbody>
          </table>}
        <p className="muted small" style={{ marginTop: 8 }}>
          <Link to={'/assessments/' + c.other.id}>Ouvrir {c.other.reference}</Link></p>
      </>}
    </div>
  )
}
