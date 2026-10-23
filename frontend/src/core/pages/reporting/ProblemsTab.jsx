import React from 'react'
import { Link } from 'react-router-dom'
import { HBars, Kpi } from './charts'
import { date, fmt } from './exportCsv'

/** Onglet Problèmes (REP-20 à REP-25). */
export default function ProblemsTab({ data }) {
  const { kpis, actions } = data
  const funnelMax = Math.max(1, ...data.funnel.map(f => f.count))
  return (
    <div className="report-body">
      <div className="kpi-row">
        <Kpi value={kpis.open} label="Problèmes ouverts" />
        <Kpi value={kpis.knownErrors} label="Erreurs connues" />
        <Kpi value={fmt(kpis.incidentsPerProblem)} label="Incidents liés par problème (moyenne)" />
        <Kpi value={kpis.rcaPending} label="Causes racines en attente de validation"
          sub="Analyse conclue, cause racine non encore validée" />
        {/* PRB-33, PRB-35 */}
        <Kpi value={fmt(kpis.rootCauseRate)} suffix=" %" label="Problèmes clos avec cause racine" />
        <Kpi value={kpis.recurrences} label="Récurrences après correctif" sub="Nouvel incident sur un problème résolu, sur la période"
          alert={kpis.recurrences > 0 ? 'À revoir : le correctif ne tient pas' : null} />
      </div>

      <div className="report-grid">
        <figure className="card chart-card">
          <figcaption><h3>Entonnoir de résolution</h3></figcaption>
          <ol className="funnel">
            {data.funnel.map(f => (
              <li key={f.stage}>
                <span className="lbl">{f.stage}</span>
                <div className="bar-track"><div className="bar" style={{ width: `${(f.count / funnelMax) * 100}%` }} /></div>
                <span className="val">{f.count}</span>
              </li>))}
          </ol>
          <p className="muted small chart-note">Où en est chaque problème aujourd'hui. Correctif planifié : une action corrective ouverte ou un changement relié.</p>
        </figure>
        <HBars title="Top 5 des services générant des problèmes"
          data={data.topServices.map(s => ({ label: s.label, count: s.count, hint: `· ${s.incidents} incident(s) lié(s)` }))}
          empty="Aucun problème rattaché à un service." />
      </div>

      <div className="card">
        <h3 className="card-title">Actions correctives</h3>
        <div className="mini-kpis">
          <span><b>{actions.open}</b> ouverte(s)</span>
          <span className={actions.overdue ? 'is-bad' : ''}><b>{actions.overdue}</b> en retard</span>
          <span><b>{actions.done}</b> terminée(s) sur la période</span>
        </div>
        {actions.overdueList.length === 0 ? <p className="muted small">Aucune action en retard.</p> : (
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Action</th><th>Problème</th><th>Échéance</th><th>Responsable (R)</th></tr></thead>
              <tbody>{actions.overdueList.map(a => (
                <tr key={a.id}>
                  <td>{a.title}</td>
                  <td><Link to={`/problems/${a.problem.id}`}>{a.problem.reference}</Link></td>
                  <td>{date(a.dueDate)}</td>
                  <td>{a.responsibles.join(', ') || '—'}</td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </div>

      {/* PRB-34 : problèmes qui regroupent le plus d'incidents. */}
      <div className="card">
        <h3 className="card-title">Problèmes aux incidents les plus nombreux</h3>
        {(data.topProblems ?? []).length === 0 ? <p className="muted small">Aucun incident relié à un problème.</p> : (
          <table className="compact">
            <thead><tr><th>Problème</th><th>Statut</th><th>Incidents reliés</th></tr></thead>
            <tbody>{data.topProblems.map(p => (
              <tr key={p.id}><td><Link to={`/problems/${p.id}`}>{p.reference}</Link> {p.title}</td><td>{p.status}</td><td>{p.incidents}</td></tr>))}</tbody>
          </table>)}
      </div>
    </div>
  )
}

export const problemsCsv = (d) => [
  { title: 'Indicateurs', header: ['Indicateur', 'Valeur'], rows: [
    ['Problèmes ouverts', d.kpis.open], ['Erreurs connues', d.kpis.knownErrors],
    ['Incidents liés par problème', d.kpis.incidentsPerProblem], ['Causes racines en attente de validation', d.kpis.rcaPending],
    ['Problèmes clos avec cause racine (%)', d.kpis.rootCauseRate], ['Récurrences après correctif', d.kpis.recurrences]] },
  { title: 'Problèmes aux incidents les plus nombreux', header: ['Référence', 'Titre', 'Statut', 'Incidents reliés'], rows: (d.topProblems ?? []).map(p => [p.reference, p.title, p.status, p.incidents]) },
  { title: 'Entonnoir de résolution', header: ['Étape', 'Problèmes'], rows: d.funnel.map(f => [f.stage, f.count]) },
  { title: 'Top 5 des services', header: ['Service', 'Problèmes', 'Incidents liés'], rows: d.topServices.map(s => [s.label, s.count, s.incidents]) },
  { title: 'Actions correctives', header: ['Ouvertes', 'En retard', 'Terminées sur la période'], rows: [[d.actions.open, d.actions.overdue, d.actions.done]] },
  { title: 'Actions en retard', header: ['Action', 'Problème', 'Échéance', 'Responsable'],
    rows: d.actions.overdueList.map(a => [a.title, a.problem.reference, date(a.dueDate), a.responsibles.join(', ')]) },
]
