import React from 'react'
import { Link } from 'react-router-dom'
import { HBars, Kpi } from './charts'
import { date, fmt } from './exportCsv'

/** Onglet Demandes (REP-60 à REP-63 : indicateurs REQ-40 à REQ-43). */
export default function RequestsTab({ data }) {
  const { kpis } = data
  return (
    <div className="report-body">
      <div className="kpi-row">
        <Kpi value={fmt(kpis.fulfillmentHours)} suffix=" h" label="Délai moyen de traitement" sub={`${kpis.fulfilled} demande(s) satisfaite(s)`} />
        <Kpi value={fmt(kpis.onTime)} suffix=" %" label="Respect des délais" sub={`sur ${kpis.dated} demande(s) à échéance`} />
        <Kpi value={fmt(kpis.approvalRate)} suffix=" %" label="Taux d’approbation" sub={`${kpis.decisions} décision(s)`} />
        <Kpi value={fmt(kpis.satisfaction)} suffix=" / 5" label="Satisfaction moyenne" sub={`${kpis.ratings} avis`} />
        <Kpi value={kpis.overdue} label="Demandes en retard" sub={`${kpis.open} ouverte(s)`} />
      </div>

      <div className="report-grid">
        <HBars title="Avis de satisfaction, par note" data={data.scores.map(s => ({ label: `${s.score} / 5`, count: s.count }))} />
      </div>

      <div className="card">
        <h3 className="card-title">Par modèle du catalogue</h3>
        {data.byItem.length === 0 ? <p className="muted small">Aucune demande sur la période.</p> : (
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Modèle</th><th>Créées</th><th>Délai moyen</th><th>Respect des délais</th><th>Approbation</th><th>Satisfaction</th></tr></thead>
              <tbody>{data.byItem.map(i => (
                <tr key={i.item}>
                  <td>{i.item}</td>
                  <td>{i.created}</td>
                  <td>{i.fulfillmentHours == null ? '—' : `${fmt(i.fulfillmentHours)} h`}</td>
                  <td>{i.onTime == null ? '—' : `${fmt(i.onTime)} %`}</td>
                  <td>{i.approvalRate == null ? '—' : `${fmt(i.approvalRate)} %`}</td>
                  <td>{i.satisfaction == null ? '—' : `${fmt(i.satisfaction)} / 5`}</td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </div>

      <div className="card">
        <h3 className="card-title">Demandes en retard</h3>
        {data.watch.length === 0 ? <p className="muted small">Aucune demande en retard.</p> : (
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Demande</th><th>Modèle</th><th>Statut</th><th>Responsable</th><th>Échéance</th></tr></thead>
              <tbody>{data.watch.map(w => (
                <tr key={w.id}>
                  <td><Link to={`/requests/${w.id}`}>{w.reference}</Link> {w.title}</td>
                  <td>{w.item}</td>
                  <td>{w.status}</td>
                  <td>{w.ownerDisplayName ?? '—'}</td>
                  <td>{date(w.dueDate)}</td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}

export const requestsCsv = (d) => [
  { title: 'Indicateurs', header: ['Indicateur', 'Valeur'], rows: [
    ['Délai moyen de traitement (heures)', d.kpis.fulfillmentHours], ['Demandes satisfaites', d.kpis.fulfilled],
    ['Respect des délais (%)', d.kpis.onTime], ['Demandes à échéance', d.kpis.dated],
    ['Taux d’approbation (%)', d.kpis.approvalRate], ['Décisions', d.kpis.decisions],
    ['Satisfaction moyenne (sur 5)', d.kpis.satisfaction], ['Avis', d.kpis.ratings],
    ['Demandes ouvertes', d.kpis.open], ['Demandes en retard', d.kpis.overdue]] },
  { title: 'Par modèle du catalogue', header: ['Modèle', 'Créées', 'Délai moyen (heures)', 'Respect des délais (%)', 'Approbation (%)', 'Satisfaction (sur 5)'],
    rows: d.byItem.map(i => [i.item, i.created, i.fulfillmentHours, i.onTime, i.approvalRate, i.satisfaction]) },
  { title: 'Avis par note', header: ['Note', 'Avis'], rows: d.scores.map(s => [s.score, s.count]) },
  { title: 'Demandes en retard', header: ['Référence', 'Titre', 'Modèle', 'Statut', 'Responsable', 'Échéance'],
    rows: d.watch.map(w => [w.reference, w.title, w.item, w.status, w.ownerDisplayName ?? '', date(w.dueDate)]) },
]
