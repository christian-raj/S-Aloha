import React, { useState } from 'react'
import { Link } from 'react-router-dom'
import { HBars, Kpi, LineChart } from './charts'
import { date, dateTime, fmt } from './exportCsv'

const SLA_TONE = { 'SLA dépassé': 't-bad', 'SLA en risque': 't-wait', 'Dans le SLA': 't-done', 'Sans SLA': 't-closed' }

/** Onglet Incidents (REP-10 à REP-15). */
export default function IncidentsTab({ data }) {
  const { kpis } = data
  const [filter, setFilter] = useState('')
  const [q, setQ] = useState('')
  const watch = data.watch.filter(w =>
    (!filter || (filter === 'major' ? w.isMajor : w.sla === filter))
    && (!q || `${w.reference} ${w.title} ${w.affectedService}`.toLowerCase().includes(q.toLowerCase())))
  return (
    <div className="report-body">
      <div className="kpi-row">
        <Kpi value={fmt(kpis.mttrHours)} suffix=" h" label="MTTR — délai moyen de résolution" sub={`${kpis.resolved} incident(s) résolu(s)`} />
        <Kpi value={fmt(kpis.mttdHours)} suffix=" h" label="MTTD — délai moyen de détection"
          sub={kpis.mttdMeasured ? `sur ${kpis.mttdMeasured} incident(s) au début constaté` : 'Début constaté jamais renseigné'} />
        <Kpi value={fmt(kpis.fcr)} suffix=" %" label="Résolution au premier contact (FCR)" />
        <Kpi value={kpis.open} label="Incidents ouverts" />
      </div>

      <div className="report-grid">
        <LineChart title="Burn-up : créés et résolus, en cumul" days={data.burnUp} series={[
          { key: 'created', label: 'Créés (cumul)', short: 'créés', slot: 1 },
          { key: 'resolved', label: 'Résolus (cumul)', short: 'résolus', slot: 3 },
        ]} />
        <HBars title="Backlog ouvert, par ancienneté" data={data.backlog.map(b => ({ label: b.bucket, count: b.count }))}
          note="Tous les incidents ouverts, quelle que soit la période." />
        <HBars title="Incidents créés, par catégorie" data={data.byCategory.map(c => ({ label: c.label, count: c.count }))} />
        <HBars title="Incidents créés, par service" data={data.byService.map(c => ({ label: c.label, count: c.count }))} />
      </div>

      <div className="card">
        <div className="card-head">
          <h3 className="card-title">Incidents à suivre : majeurs, SLA en risque ou dépassé</h3>
          <div className="inline-filters no-print">
            <select aria-label="Filtrer les incidents à suivre" value={filter} onChange={e => setFilter(e.target.value)}>
              <option value="">Tous</option>
              <option value="major">Majeurs</option>
              <option value="SLA dépassé">SLA dépassé</option>
              <option value="SLA en risque">SLA en risque</option>
            </select>
            <input type="search" placeholder="Référence, titre, service…" aria-label="Rechercher" value={q} onChange={e => setQ(e.target.value)} />
          </div>
        </div>
        {watch.length === 0 ? <p className="muted small">Aucun incident à suivre.</p> : (
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Incident</th><th>Priorité</th><th>Statut</th><th>Responsable</th><th>Échéance</th><th>SLA</th></tr></thead>
              <tbody>{watch.map(w => (
                <tr key={w.id}>
                  <td><Link to={`/incidents/${w.id}`}>{w.reference}</Link> {w.title}
                    {w.isMajor && <span className="badge t-bad" style={{ marginLeft: 6 }}>Majeur</span>}</td>
                  <td><span className={`badge ${w.priority}`}>{w.priority}</span></td>
                  <td>{w.status}</td>
                  <td>{w.ownerDisplayName ?? '—'}</td>
                  <td>{w.resolutionDueAt ? dateTime(w.resolutionDueAt) : '—'}</td>
                  <td><span className={`badge ${SLA_TONE[w.sla]}`}>{w.sla}</span></td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}

export const incidentsCsv = (d) => [
  { title: 'Indicateurs', header: ['Indicateur', 'Valeur'], rows: [
    ['MTTR (heures)', d.kpis.mttrHours], ['MTTD (heures)', d.kpis.mttdHours], ['Incidents au début constaté', d.kpis.mttdMeasured],
    ['FCR (%)', d.kpis.fcr], ['Incidents résolus', d.kpis.resolved], ['Incidents ouverts', d.kpis.open]] },
  { title: 'Backlog par ancienneté', header: ['Ancienneté', 'Incidents'], rows: d.backlog.map(b => [b.bucket, b.count]) },
  { title: 'Burn-up', header: ['Jour', 'Créés (cumul)', 'Résolus (cumul)'], rows: d.burnUp.map(b => [date(b.date), b.created, b.resolved]) },
  { title: 'Par catégorie', header: ['Catégorie', 'Incidents'], rows: d.byCategory.map(c => [c.label, c.count]) },
  { title: 'Par service', header: ['Service', 'Incidents'], rows: d.byService.map(c => [c.label, c.count]) },
  { title: 'Incidents à suivre', header: ['Référence', 'Titre', 'Priorité', 'Majeur', 'Statut', 'Responsable', 'Échéance', 'SLA', 'Service'],
    rows: d.watch.map(w => [w.reference, w.title, w.priority, w.isMajor ? 'Oui' : 'Non', w.status, w.ownerDisplayName, dateTime(w.resolutionDueAt), w.sla, w.affectedService]) },
]
