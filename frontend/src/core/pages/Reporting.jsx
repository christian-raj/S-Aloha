import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api'

function Bars({ title, data, labelKey, max }) {
  return (
    <div className="card">
      <h3 style={{ color: 'var(--navy)', marginBottom: 14 }}>{title}</h3>
      {data.length === 0 && <p style={{ color: 'var(--muted)', fontSize: 13 }}>Aucune donnée pour le moment.</p>}
      {data.map(d => (
        <div className="bar-row" key={d[labelKey]}>
          <span className="lbl">{d[labelKey] || '—'}</span>
          <div className="bar-track"><div className="bar" style={{ width: (d.count / max * 100) + '%' }} /></div>
          <span className="val">{d.count}</span>
        </div>
      ))}
    </div>
  )
}

export default function Reporting() {
  const [s, setS] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.reports.summary().then(setS).catch(e => setError(e.message)) }, [])
  if (error) return <div className="error">{error}</div>
  if (!s) return <p>Chargement…</p>

  const max = arr => Math.max(1, ...arr.map(x => x.count))
  return (
    <>
      <h1 className="page-title">Reporting</h1>
      <p className="page-sub">Indicateurs des processus ITIL : problèmes, incidents, changements et volumétrie par processus.</p>

      <div className="kpi-row">
        <div className="kpi"><div className="n">{s.majorIncidentsOpen}</div><div className="l">Incidents majeurs ouverts</div></div>
        <div className="kpi"><div className="n">{s.incidentMttrHours ?? '—'}</div><div className="l">MTTR incidents (heures)</div></div>
        <div className="kpi"><div className="n">{s.changeSuccessRate != null ? s.changeSuccessRate + ' %' : '—'}</div><div className="l">Changements réussis</div></div>
        <div className="kpi"><div className="n">{s.processes.reduce((n, p) => n + p.total, 0)}</div><div className="l">Enregistrements (hors problèmes)</div></div>
      </div>

      <div className="kpi-row">
        <div className="kpi"><div className="n">{s.openProblems}</div><div className="l">Problèmes ouverts</div></div>
        <div className="kpi"><div className="n">{s.knownErrors}</div><div className="l">Erreurs connues</div></div>
        <div className="kpi"><div className="n">{s.overdueActions.length}</div><div className="l">Actions en retard</div></div>
        <div className="kpi"><div className="n">{s.mttrDays ?? '—'}</div><div className="l">MTTR (jours)</div></div>
      </div>

      <div className="kpi-row">
        <div className="kpi"><div className="n">{s.totalProblems}</div><div className="l">Problèmes (total)</div></div>
        <div className="kpi"><div className="n">{s.totalActions}</div><div className="l">Actions (total)</div></div>
        <div className="kpi"><div className="n">{s.totalAnalyses}</div><div className="l">Analyses RCA</div></div>
        <div className="kpi"><div className="n">{s.contributors}</div><div className="l">Déclarants distincts</div></div>
      </div>

      <div className="grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <Bars title="Problèmes par statut" data={s.problemsByStatus} labelKey="status" max={max(s.problemsByStatus)} />
        <Bars title="Problèmes par priorité" data={s.problemsByPriority} labelKey="priority" max={max(s.problemsByPriority)} />
        <Bars title="Problèmes par catégorie" data={s.problemsByCategory} labelKey="category" max={max(s.problemsByCategory)} />
        <div className="card">
          <h3 style={{ color: 'var(--navy)', marginBottom: 14 }}>Actions en retard</h3>
          {s.overdueActions.length === 0 && <p style={{ color: 'var(--muted)', fontSize: 13 }}>Aucune action en retard. 👍</p>}
          {s.overdueActions.map(a => (
            <div key={a.id} style={{ padding: '8px 0', borderBottom: '1px solid var(--line)', fontSize: 13 }}>
              <b>{a.title}</b><br />
              Échéance : {new Date(a.dueDate).toLocaleDateString('fr-FR')} — R : {a.responsibles.join(', ') || '—'}
            </div>
          ))}
        </div>
      </div>

      <h2 style={{ color: 'var(--navy)', fontSize: 17, margin: '28px 0 12px' }}>Volumétrie par processus</h2>
      <div className="grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
        {s.processes.map(p => (
          <Bars key={p.process} title={`${p.process} (${p.total})`} data={p.byStatus} labelKey="status" max={max(p.byStatus)} />
        ))}
      </div>
      <p style={{ marginTop: 20 }}><Link to="/problems">Voir tous les problèmes →</Link></p>
    </>
  )
}
