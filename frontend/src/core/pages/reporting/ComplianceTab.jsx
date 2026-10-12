import React from 'react'
import { Link } from 'react-router-dom'
import { HBars, Kpi } from './charts'
import { date, fmt } from './exportCsv'

const rateTone = (r) => r == null ? 't-closed' : r >= 95 ? 't-done' : r >= 80 ? 't-wait' : 't-bad'

/**
 * Onglet Conformité & métriques (REP-40 à REP-43) : NIS 2, niveaux de service,
 * amélioration continue et connaissances. Les déclarations réglementaires y
 * prendront place avec leur module.
 */
export default function ComplianceTab({ data }) {
  const { nis2, nis2Pending = [], sla, improvements, knowledge } = data
  return (
    <div className="report-body">
      <div className="card">
        <h3 className="card-title">NIS 2 — dernière évaluation validée</h3>
        {!nis2 ? (
          <p className="muted small">Aucune évaluation validée : le score n'est retenu qu'une fois l'évaluation validée.{' '}
            <Link to="/assessments">Évaluer la conformité NIS 2 →</Link></p>
        ) : (
          <>
            <p className="small"><Link to={`/assessments/${nis2.id}`}>{nis2.reference}</Link> {nis2.title} — validée le {date(nis2.validatedAt)}</p>
            <div className="kpi-row">
              <Kpi value={fmt(nis2.percent, 0)} suffix=" %" label="Score de conformité" />
              <Kpi value={nis2.maturity ? `${nis2.maturity.level}` : null} label={nis2.maturity ? `Maturité : ${nis2.maturity.name}` : 'Maturité'} />
              <Kpi value={nis2.gaps} label="Exigences en écart" />
              <Kpi value={nis2.improvements} label="Améliorations reliées" />
            </div>
            <HBars title="Score par pilier" unit=" %" max={100}
              data={nis2.pillars.map(p => ({ label: p.pillar, count: Math.round(p.percent ?? 0) }))} />
          </>
        )}
        {nis2Pending.length > 0 && (
          <p className="small chart-note">En cours : {nis2Pending.map((a, i) => (
            <span key={a.id}>{i > 0 && ', '}<Link to={`/assessments/${a.id}`}>{a.reference}</Link> ({a.status})</span>))}</p>
        )}
      </div>

      <div className="report-grid">
        <div className="card">
          <h3 className="card-title">Respect des SLA, par service</h3>
          {sla.byService.length === 0 ? <p className="muted small">Aucun incident résolu avec SLA sur la période.</p> : (
            <div className="table-scroll">
              <table className="compact">
                <thead><tr><th>Service</th><th>Résolus</th><th>Dans le SLA</th><th>Taux</th></tr></thead>
                <tbody>{sla.byService.map(s => (
                  <tr key={s.service}><td>{s.service}</td><td>{s.resolved}</td><td>{s.withinSla}</td>
                    <td><span className={`badge ${rateTone(s.rate)}`}>{fmt(s.rate)} %</span></td></tr>))}</tbody>
              </table>
            </div>
          )}
          <h4 className="sub-title">SLA à revoir</h4>
          {sla.toReview.length === 0 ? <p className="muted small">Aucun SLA en vigueur dont la revue est échue.</p> : (
            <ul className="plain-list">{sla.toReview.map(a => (
              <li key={a.id}><Link to={`/agreements/${a.id}`}>{a.reference}</Link> {a.title} — {a.service}, revue prévue le {date(a.reviewDate)}</li>))}</ul>
          )}
        </div>
        <HBars title="Améliorations, par statut" data={improvements.byStatus.map(s => ({ label: s.status, count: s.count }))}
          note={`${improvements.completed} amélioration(s) réalisée(s) sur la période.`} />
      </div>

      <div className="kpi-row">
        <Kpi value={knowledge.published} label="Articles publiés" />
        <Kpi value={knowledge.publishedInPeriod} label="Publiés sur la période" />
        <Kpi value={knowledge.reviewOverdue} label="Revues d'articles en retard" />
        <Kpi value={knowledge.drafts} label="Brouillons" />
      </div>
    </div>
  )
}

export const complianceCsv = (d) => [
  ...(d.nis2 ? [
    { title: `NIS 2 — ${d.nis2.reference} ${d.nis2.title}`, header: ['Indicateur', 'Valeur'], rows: [
      ['Score (%)', d.nis2.percent], ['Maturité', d.nis2.maturity ? `${d.nis2.maturity.level} — ${d.nis2.maturity.name}` : ''],
      ['Exigences en écart', d.nis2.gaps], ['Améliorations reliées', d.nis2.improvements]] },
    { title: 'NIS 2 — score par pilier', header: ['Pilier', 'Score (%)'], rows: d.nis2.pillars.map(p => [p.pillar, p.percent]) },
  ] : []),
  { title: 'Respect des SLA par service', header: ['Service', 'Résolus', 'Dans le SLA', 'Taux (%)'],
    rows: d.sla.byService.map(s => [s.service, s.resolved, s.withinSla, s.rate]) },
  { title: 'SLA à revoir', header: ['Référence', 'Titre', 'Service', 'Revue prévue'], rows: d.sla.toReview.map(a => [a.reference, a.title, a.service, date(a.reviewDate)]) },
  { title: 'Améliorations par statut', header: ['Statut', 'Nombre'], rows: d.improvements.byStatus.map(s => [s.status, s.count]) },
  { title: 'Connaissances', header: ['Indicateur', 'Valeur'], rows: [
    ['Articles publiés', d.knowledge.published], ['Publiés sur la période', d.knowledge.publishedInPeriod],
    ['Revues en retard', d.knowledge.reviewOverdue], ['Brouillons', d.knowledge.drafts]] },
]
