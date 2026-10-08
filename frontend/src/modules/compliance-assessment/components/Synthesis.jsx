import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'

const pct = (p) => (p === null || p === undefined ? '—' : `${p.toLocaleString('fr-FR')} %`)

function Bar({ label, percent, sub, stacked }) {
  return (
    <div className={'bar-row' + (stacked ? ' stacked' : '')}>
      <span className="lbl" title={label}>{label}</span>
      <div className="bar-track"><div className="bar" style={{ width: (percent ?? 0) + '%' }} /></div>
      <span className="val">{pct(percent)}</span>
      {sub && <span className="muted small" style={{ minWidth: 60 }}>{sub}</span>}
    </div>
  )
}

/**
 * Synthèse d'une évaluation (NIS-10 à NIS-13) : score, maturité, couverture,
 * scores par pilier et par objectif, écarts — chacun pouvant devenir une
 * action d'amélioration reliée (NIS-15).
 */
export default function Synthesis({ assessment, onChanged }) {
  const [s, setS] = useState(null)
  const [error, setError] = useState('')
  const [created, setCreated] = useState({})
  const load = () => api.assessments.score(assessment.id).then(setS).catch(e => setError(e.message))
  useEffect(() => { load() }, [assessment.id])
  if (error) return <div className="error">{error}</div>
  if (!s) return <p>Chargement…</p>

  const act = (code) => api.assessments.createImprovement(assessment.id, code)
    .then(r => { setCreated(c => ({ ...c, [code]: r })); onChanged() })
    .catch(e => setCreated(c => ({ ...c, [code]: { error: e.message } })))

  return (
    <>
      <div className="kpi-row">
        <div className="kpi"><div className="n">{pct(s.percent)}</div><div className="l">Score de conformité</div></div>
        <div className="kpi"><div className="n">{s.maturity ? `${s.maturity.level}/5` : '—'}</div>
          <div className="l">Maturité{s.maturity && ` : ${s.maturity.name}`}</div></div>
        <div className="kpi"><div className="n">{s.answered}/{s.applicable}</div><div className="l">Exigences répondues</div></div>
        <div className="kpi"><div className="n">{s.gaps.length}</div><div className="l">Écarts</div></div>
      </div>

      <div className="row2" style={{ alignItems: 'start' }}>
        <div className="card">
          <h3 style={{ marginBottom: 12 }}>Par pilier</h3>
          {s.pillars.map(p => <Bar key={p.pillar} label={p.pillar} percent={p.percent} />)}
        </div>
        <div className="card">
          <h3 style={{ marginBottom: 12 }}>Par objectif de sécurité</h3>
          {s.objectives.map(o => <Bar key={o.id} stacked label={`${o.id}. ${o.title}`} percent={o.percent} sub={`${o.answered}/${o.applicable}`} />)}
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3 style={{ marginBottom: 4 }}>Écarts ({s.gaps.length})</h3>
        <p className="muted small" style={{ marginBottom: 10 }}>
          Exigences notées sous 3, de l'écart le plus fort au plus faible. Chacune peut ouvrir une action au registre d'amélioration continue.</p>
        {s.gaps.length === 0
          ? <p className="muted small">Aucun écart sur les exigences notées.</p>
          : <table>
            <thead><tr><th>Exigence</th><th>Thématique</th><th>Score</th><th>Écart</th><th /></tr></thead>
            <tbody>{s.gaps.map(g => {
              const c = created[g.code]
              return (
                <tr key={g.code}>
                  <td><b>{g.code}</b></td><td>{g.theme}</td><td>{g.score}/3</td><td>{g.gap}</td>
                  <td style={{ textAlign: 'right' }}>
                    {c?.reference
                      ? <Link to={'/improvements/' + c.id}>{c.reference} →</Link>
                      : <button className="btn ghost small" onClick={() => act(g.code)}>Créer une action d'amélioration</button>}
                    {c?.error && <div className="error small" style={{ margin: '6px 0 0' }}>{c.error}</div>}
                  </td>
                </tr>)
            })}</tbody>
          </table>}
      </div>
    </>
  )
}
