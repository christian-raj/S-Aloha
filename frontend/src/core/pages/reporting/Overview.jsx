import React from 'react'
import { Link } from 'react-router-dom'
import { Kpi, LineChart, Tolerance } from './charts'
import { date, fmt } from './exportCsv'

const LEVEL_TONE = { Bon: 't-done', Dégradé: 't-wait', Critique: 't-bad' }
const PRIORITY = { Critique: 'P1', Élevée: 'P2', Moyenne: 'P3', Basse: 'P4' }

/** Onglet Synthèse globale (REP-01 à REP-06). */
export default function Overview({ data }) {
  const { health, kpis } = data
  return (
    <div className="report-body">
      <section className={`health ${LEVEL_TONE[health.level]}`} aria-label="Score de santé">
        <div className="health-score"><b>{health.score}</b><span>/ 100</span></div>
        <div className="health-body">
          <div className="health-title">Santé des services : <span className={`badge ${LEVEL_TONE[health.level]}`}>{health.level}</span></div>
          <p className="small">
            {health.factors.some(f => f.penalty > 0)
              ? health.factors.filter(f => f.penalty > 0).map(f => `${f.label} (${f.count}) : −${f.penalty}`).join(' · ')
              : 'Aucun facteur de dégradation sur la période.'}
          </p>
          <details className="small">
            <summary>Comment ce score est calculé</summary>
            <p>100 points, moins une pénalité plafonnée par facteur : Bon à partir de 80, Dégradé de 50 à 79, Critique en dessous.</p>
            <ul>{health.factors.map(f => <li key={f.label}>{f.label} : {f.rule}</li>)}</ul>
          </details>
        </div>
      </section>

      <div className="kpi-row">
        <Kpi value={kpis.majorIncidentsOpen} label="Incidents majeurs en cours"
          alert={kpis.majorIncidentsOpen > 0 ? 'Alerte : à traiter en priorité' : null} />
        <Kpi value={fmt(kpis.slaRate)} suffix=" %" label="Résolution dans le SLA"
          sub={kpis.slaMeasured ? `sur ${kpis.slaMeasured} incident(s) résolu(s) avec SLA` : 'Aucun incident résolu avec SLA'} />
        <Kpi value={kpis.changesThisWeek} label="Changements planifiés cette semaine" />
        <Kpi value={kpis.projectsAtRisk} label="Projets à risque ou en retard"
          sub={data.projectsAtRisk.slice(0, 3).map(p => p.reference).join(', ') || null} />
      </div>

      <div className="report-grid wide">
        <LineChart title="Volumétrie croisée, par jour" days={data.volume} series={[
          { key: 'incidents', label: 'Incidents créés', short: 'inc.', slot: 1 },
          { key: 'requests', label: 'Demandes créées', short: 'dem.', slot: 2 },
          { key: 'changes', label: 'Changements mis en œuvre', short: 'chg.', slot: 3 },
        ]} />
        <figure className="card chart-card">
          <figcaption><h3>Matrice d'impact des priorités</h3></figcaption>
          <p className="muted small">Incidents et problèmes ouverts, par priorité.</p>
          <div className="priority-matrix">
            {data.priorityMatrix.map(m => (
              <div key={m.level} className="pm-cell">
                <span className={`badge ${PRIORITY[m.level]}`}>{m.level}</span>
                <div><b>{m.incidents}</b> incident(s)</div>
                <div><b>{m.problems}</b> problème(s)</div>
              </div>
            ))}
          </div>
        </figure>
      </div>

      {data.projectsAtRisk.length > 0 && (
        <div className="card">
          <h3 className="card-title">Projets à risque ou en retard</h3>
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Projet</th><th>Délai</th><th>Coût</th></tr></thead>
              <tbody>{data.projectsAtRisk.map(p => (
                <tr key={p.id}>
                  <td><Link to={`/projects/${p.id}`}>{p.reference}</Link> {p.title}</td>
                  <td><Tolerance value={p.time} /></td><td><Tolerance value={p.cost} /></td>
                </tr>))}</tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}

export const overviewCsv = (d) => [
  { title: 'Score de santé', header: ['Facteur', 'Nombre', 'Pénalité', 'Règle'],
    rows: [...d.health.factors.map(f => [f.label, f.count, f.penalty, f.rule]), ['Score', '', d.health.score, d.health.level]] },
  { title: 'Indicateurs clés', header: ['Indicateur', 'Valeur'], rows: [
    ['Incidents majeurs en cours', d.kpis.majorIncidentsOpen],
    ['Résolution dans le SLA (%)', d.kpis.slaRate],
    ['Incidents résolus avec SLA', d.kpis.slaMeasured],
    ['Changements planifiés cette semaine', d.kpis.changesThisWeek],
    ['Projets à risque ou en retard', d.kpis.projectsAtRisk]] },
  { title: 'Volumétrie croisée', header: ['Jour', 'Incidents', 'Demandes', 'Changements'],
    rows: d.volume.map(v => [date(v.date), v.incidents, v.requests, v.changes]) },
  { title: 'Matrice des priorités', header: ['Priorité', 'Incidents ouverts', 'Problèmes ouverts'],
    rows: d.priorityMatrix.map(m => [m.level, m.incidents, m.problems]) },
  { title: 'Projets à risque', header: ['Référence', 'Titre', 'Délai', 'Coût'],
    rows: d.projectsAtRisk.map(p => [p.reference, p.title, p.time, p.cost]) },
]
