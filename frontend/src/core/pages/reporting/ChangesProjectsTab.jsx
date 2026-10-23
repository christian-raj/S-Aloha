import React from 'react'
import { Link } from 'react-router-dom'
import { HBars, Kpi, Tolerance } from './charts'
import { date, dateTime, fmt } from './exportCsv'

const DAY = 86400000
const short = (d) => d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit' })
const RISK_TONE = (score) => score >= 15 ? 't-bad' : score >= 8 ? 't-wait' : 't-done'

/** Calendrier simplifié des changements à venir (REP-33) : une barre par changement sur 30 jours. */
function Gantt({ items }) {
  const start = new Date(); start.setHours(0, 0, 0, 0)
  const span = 30 * DAY
  const pos = (d) => Math.max(0, Math.min(100, ((new Date(d) - start) / span) * 100))
  const weeks = [0, 7, 14, 21, 28]
  return (
    <figure className="card chart-card">
      <figcaption><h3>Changements des 30 prochains jours</h3></figcaption>
      <div className="chart-legend">
        <span><i className="swatch c3" />Standard</span><span><i className="swatch c1" />Normal</span><span><i className="swatch c2" />Urgent</span>
      </div>
      {items.length === 0 ? <p className="muted small">Aucun changement planifié.</p> : (
        <div className="gantt" role="list">
          <div className="gantt-scale" aria-hidden>
            {weeks.map(w => <span key={w} style={{ left: `${(w / 30) * 100}%` }}>{short(new Date(start.getTime() + w * DAY))}</span>)}
          </div>
          {items.map(c => {
            const left = pos(c.plannedStart)
            const width = Math.max(1.2, pos(c.plannedEnd ?? c.plannedStart) - left)
            return (
              <div className="gantt-row" role="listitem" key={c.id}>
                <div className="gantt-head">
                  <span className="lbl"><Link to={`/changes/${c.id}`}>{c.reference}</Link> {c.title}</span>
                  <span className="val">{c.changeType} · {dateTime(c.plannedStart)}</span>
                </div>
                <div className="gantt-track" title={`${dateTime(c.plannedStart)} → ${dateTime(c.plannedEnd)}`}>
                  <div className={`gantt-bar type-${c.changeType}`} style={{ left: `${left}%`, width: `${width}%` }} />
                </div>
              </div>)
          })}
        </div>
      )}
    </figure>
  )
}

/** Matrice 5 × 5 des risques projets ouverts (REP-34) : probabilité en lignes, impact en colonnes. */
function RiskMatrix({ cells }) {
  const at = (p, i) => cells.find(c => c.probability === p && c.impact === i)
  return (
    <figure className="card chart-card">
      <figcaption><h3>Matrice des risques projets (impact × probabilité)</h3></figcaption>
      <div className="risk-matrix" role="table" aria-label="Risques ouverts par probabilité et impact">
        {[5, 4, 3, 2, 1].map(p => (
          <div className="rm-row" role="row" key={p}>
            <span className="rm-axis" role="rowheader">P{p}</span>
            {[1, 2, 3, 4, 5].map(i => {
              const c = at(p, i)
              return (
                <span key={i} role="cell" className={`rm-cell ${RISK_TONE(p * i)}${c ? ' filled' : ''}`}
                  title={c ? c.risks.map(r => `${r.project} — ${r.title}`).join('\n') : `Score ${p * i}`}>
                  {c ? c.count : ''}
                </span>)
            })}
          </div>))}
        <div className="rm-row" role="row">
          <span className="rm-axis" />
          {[1, 2, 3, 4, 5].map(i => <span key={i} className="rm-axis" role="columnheader">I{i}</span>)}
        </div>
      </div>
      <p className="muted small chart-note">Nombre de risques ouverts par case. Teinte : score probabilité × impact (critique à partir de 15).</p>
    </figure>
  )
}

/** Onglet Changements & projets (REP-30 à REP-34). */
export default function ChangesProjectsTab({ data }) {
  const { kpis } = data
  const type = (t) => kpis.byType.find(x => x.type === t)?.count ?? 0
  return (
    <div className="report-body">
      <div className="kpi-row">
        <Kpi value={fmt(kpis.successRate)} suffix=" %" label="Taux de succès des changements"
          sub={kpis.implemented ? `sur ${kpis.implemented} changement(s) mis en œuvre` : 'Aucun changement mis en œuvre'} />
        <Kpi value={type('Urgent')} label="Changements urgents"
          sub={`${kpis.urgentShare == null ? '—' : fmt(kpis.urgentShare) + ' %'} des changements créés · ${type('Standard')} standard(s), ${type('Normal')} normal(aux)`} />
        <Kpi value={fmt(kpis.projectsProgress, 0)} suffix=" %" label="Avancement moyen des projets"
          sub={`${kpis.activeProjects} projet(s) en cours ou suspendu(s)`} />
        <Kpi value={data.projects.filter(p => p.tolerance.time === 'Hors tolérance' || p.tolerance.cost === 'Hors tolérance').length}
          label="Projets hors tolérance" />
      </div>
      {/* CHG-33 à CHG-35 */}
      <div className="kpi-row">
        <Kpi value={fmt(kpis.incidentAfterChange)} suffix=" %" label="Changements suivis d’un incident"
          sub={`${kpis.incidentAfterChangeCount ?? 0} relié(s) à un incident dans les 7 jours de leur mise en œuvre`} />
        <Kpi value={fmt(kpis.authorizationHours)} suffix=" h" label="Délai moyen d’autorisation" sub="Changements normaux et urgents" />
        <Kpi value={fmt(kpis.slotRespect)} suffix=" %" label="Respect des créneaux" sub={`sur ${kpis.slotMeasured ?? 0} changement(s) aux dates réelles saisies`} />
      </div>

      <div className="report-grid">
        <Gantt items={data.upcoming} />
        <RiskMatrix cells={data.riskMatrix} />
      </div>

      {/* CHG-32 : part des changements urgents, mois par mois. */}
      <div className="report-grid">
        <HBars title="Part des changements urgents, par mois" unit=" %" max={100}
          data={(kpis.urgentByMonth ?? []).map(m => ({ label: m.month, count: m.percent ?? 0 }))}
          note="Changements urgents / changements créés dans le mois." />
      </div>

      <div className="card">
        <h3 className="card-title">Avancement des projets</h3>
        {data.projects.length === 0 ? <p className="muted small">Aucun projet en cours.</p> : (
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Projet</th><th>Méthode</th><th>Avancement</th><th>Délai</th><th>Coût</th></tr></thead>
              <tbody>{data.projects.map(p => (
                <tr key={p.id}>
                  <td><Link to={`/projects/${p.id}`}>{p.reference}</Link> {p.title}</td>
                  <td>{p.method}</td>
                  <td>{p.progress == null ? '—' : (
                    <span className="progress"><span className="bar-track"><span className="bar" style={{ width: `${p.progress}%` }} /></span>{p.progress} %</span>)}</td>
                  <td><Tolerance value={p.tolerance.time} /></td>
                  <td><Tolerance value={p.tolerance.cost} /></td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}

export const changesProjectsCsv = (d) => [
  { title: 'Indicateurs', header: ['Indicateur', 'Valeur'], rows: [
    ['Taux de succès des changements (%)', d.kpis.successRate], ['Changements mis en œuvre', d.kpis.implemented],
    ...d.kpis.byType.map(t => [`Changements ${t.type.toLowerCase()}s créés`, t.count]),
    ['Part des changements urgents (%)', d.kpis.urgentShare ?? ''],
    ...(d.kpis.urgentByMonth ?? []).map(m => [`Changements urgents, ${m.month} (%)`, m.percent ?? '']),
    ['Changements suivis d’un incident (%)', d.kpis.incidentAfterChange ?? ''], ['Délai moyen d’autorisation (heures)', d.kpis.authorizationHours ?? ''],
    ['Respect des créneaux (%)', d.kpis.slotRespect ?? ''],
    ['Avancement moyen des projets (%)', d.kpis.projectsProgress], ['Projets en cours', d.kpis.activeProjects]] },
  { title: 'Changements des 30 prochains jours', header: ['Référence', 'Titre', 'Type', 'Risque', 'Statut', 'Début', 'Fin'],
    rows: d.upcoming.map(c => [c.reference, c.title, c.changeType, c.risk, c.status, dateTime(c.plannedStart), dateTime(c.plannedEnd)]) },
  { title: 'Projets', header: ['Référence', 'Titre', 'Méthode', 'Avancement (%)', 'Délai', 'Coût'],
    rows: d.projects.map(p => [p.reference, p.title, p.method, p.progress, p.tolerance.time, p.tolerance.cost]) },
  { title: 'Risques projets ouverts', header: ['Projet', 'Risque', 'Probabilité', 'Impact', 'Score'],
    rows: d.riskMatrix.flatMap(c => c.risks.map(r => [r.project, r.title, c.probability, c.impact, c.probability * c.impact])) },
]
