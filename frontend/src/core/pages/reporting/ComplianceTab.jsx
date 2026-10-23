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
  const { nis2, nis2Pending = [], sla, improvements, knowledge, regulatory = [], availability = [] } = data
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
              {/* NIS-30, NIS-31 */}
              <Kpi value={fmt(nis2.gapsTreatedRate, 0)} suffix=" %" label="Écarts traités" sub={`${nis2.gapsTreated ?? 0} écart(s) sur ${nis2.gaps} avec une amélioration`} />
              <Kpi value={nis2.delta == null ? null : `${nis2.delta > 0 ? '+' : ''}${fmt(nis2.delta)}`} suffix=" pts" label="Évolution du score"
                sub={nis2.previous ? <>depuis <Link to={`/assessments/${nis2.previous.id}`}>{nis2.previous.reference}</Link> ({fmt(nis2.previous.percent, 0)} %)</> : 'Aucune évaluation validée précédente'} />
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
        {/* CSI-30 */}
        <HBars title="Améliorations réalisées, par source" data={improvements.completedBySource ?? []} />
      </div>

      {/* CSI-31, CSI-32, DRG-21 */}
      <div className="kpi-row">
        <Kpi value={fmt(improvements.completionDays)} suffix=" j" label="Délai moyen de réalisation" sub="de la validation à la réalisation" />
        <Kpi value={fmt(improvements.abandonRate)} suffix=" %" label="Taux d’abandon des améliorations" />
        <Kpi value={fmt(data.qualificationHours)} suffix=" h" label="Délai moyen de qualification" sub="incidents cyber, depuis la prise de connaissance" />
      </div>

      {/* SLM-31 : disponibilité mesurée des SLA dotés d'une cible, sur la période. */}
      {availability.length > 0 && <div className="card" style={{ marginBottom: 16 }}>
        <h3 className="card-title">Disponibilité des services sous SLA</h3>
        <div className="table-scroll">
          <table className="compact">
            <thead><tr><th>SLA</th><th>Service</th><th>Disponibilité</th><th>Cible</th><th>Indisponibilité</th></tr></thead>
            <tbody>{availability.map(a => (
              <tr key={a.id}>
                <td><Link to={`/agreements/${a.id}`}>{a.reference}</Link> {a.title}</td><td>{a.service}</td>
                <td><span className={`badge ${a.met === false ? 't-bad' : 't-done'}`}>{a.percent == null ? '—' : `${fmt(a.percent, 2)} %`}</span></td>
                <td>{a.target} %</td><td className="small">{fmt(a.downtimeHours)} h, {a.majorIncidents} incident(s) majeur(s)</td>
              </tr>))}</tbody>
          </table>
        </div>
      </div>}

      {/* REP-44, DRG-20 : déclarations réglementaires de la période et respect des délais légaux. */}
      <div className="card" style={{ marginBottom: 16 }}>
        <h3 className="card-title">Déclarations réglementaires</h3>
        {regulatory.every(r => r.total === 0) ? <p className="muted small">Aucune déclaration sur la période.</p> : (
          <div className="table-scroll">
            <table className="compact">
              <thead><tr><th>Régime</th><th>Déclarations</th><th>Déclarées</th><th>En cours</th><th>Non requises</th><th>Délais tenus</th></tr></thead>
              <tbody>{regulatory.map(r => (
                <tr key={r.regime}>
                  <td><b>{r.regime}</b></td><td>{r.total}</td><td>{r.declared ?? 0}</td><td>{r.inProgress}</td><td>{r.notRequired}</td>
                  <td className="small">{r.steps.length === 0 ? '—' : r.steps.map(s => `${s.step} : ${s.onTime}/${s.sent} (${s.percent} %)`).join(' · ')}</td>
                </tr>))}</tbody>
            </table>
          </div>)}
      </div>

      <div className="kpi-row">
        <Kpi value={knowledge.published} label="Articles publiés" />
        <Kpi value={knowledge.publishedInPeriod} label="Publiés sur la période" />
        <Kpi value={knowledge.reviewOverdue} label="Revues d'articles en retard" />
        <Kpi value={knowledge.drafts} label="Brouillons" />
        {/* KB-31, KB-32 */}
        <Kpi value={fmt(knowledge.upToDate, 0)} suffix=" %" label="Articles à jour" sub="publiés, revue non échue" />
        <Kpi value={fmt(knowledge.incidentsWithArticle, 0)} suffix=" %" label="Incidents résolus avec un article" />
      </div>
      {/* KB-30 */}
      {(knowledge.topUsed ?? []).length > 0 && <HBars title="Articles les plus utilisés (incidents et demandes reliés)"
        data={knowledge.topUsed.map(a => ({ label: `${a.reference} ${a.title}`, count: a.uses }))} />}
    </div>
  )
}

export const complianceCsv = (d) => [
  { title: 'Déclarations réglementaires', header: ['Régime', 'Déclarations', 'En cours', 'Non requises', 'Étape', 'Envois', 'Dans les délais', '%'],
    rows: (d.regulatory ?? []).flatMap(r => (r.steps.length ? r.steps : [{ step: '', sent: 0, onTime: 0, percent: '' }])
      .map(s => [r.regime, r.total, r.inProgress, r.notRequired, s.step, s.sent, s.onTime, s.percent])) },
  { title: 'Déclarations par issue', header: ['Régime', 'Déclarées', 'Non requises'], rows: (d.regulatory ?? []).map(r => [r.regime, r.declared ?? 0, r.notRequired]) },
  ...(d.nis2 ? [
    { title: `NIS 2 — ${d.nis2.reference} ${d.nis2.title}`, header: ['Indicateur', 'Valeur'], rows: [
      ['Score (%)', d.nis2.percent], ['Maturité', d.nis2.maturity ? `${d.nis2.maturity.level} — ${d.nis2.maturity.name}` : ''],
      ['Exigences en écart', d.nis2.gaps], ['Améliorations reliées', d.nis2.improvements],
      ['Écarts traités (%)', d.nis2.gapsTreatedRate ?? ''], ['Évolution du score (points)', d.nis2.delta ?? '']] },
    { title: 'NIS 2 — score par pilier', header: ['Pilier', 'Score (%)'], rows: d.nis2.pillars.map(p => [p.pillar, p.percent]) },
  ] : []),
  { title: 'Respect des SLA par service', header: ['Service', 'Résolus', 'Dans le SLA', 'Taux (%)'],
    rows: d.sla.byService.map(s => [s.service, s.resolved, s.withinSla, s.rate]) },
  { title: 'SLA à revoir', header: ['Référence', 'Titre', 'Service', 'Revue prévue'], rows: d.sla.toReview.map(a => [a.reference, a.title, a.service, date(a.reviewDate)]) },
  { title: 'Améliorations par statut', header: ['Statut', 'Nombre'], rows: d.improvements.byStatus.map(s => [s.status, s.count]) },
  { title: 'Améliorations réalisées par source', header: ['Source', 'Nombre'], rows: (d.improvements.completedBySource ?? []).map(s => [s.label, s.count]) },
  { title: 'Amélioration et déclarations', header: ['Indicateur', 'Valeur'], rows: [
    ['Délai moyen de réalisation (jours)', d.improvements.completionDays ?? ''], ['Taux d’abandon (%)', d.improvements.abandonRate ?? ''],
    ['Délai moyen de qualification (heures)', d.qualificationHours ?? '']] },
  { title: 'Disponibilité des services sous SLA', header: ['SLA', 'Service', 'Disponibilité (%)', 'Cible (%)', 'Indisponibilité (heures)'],
    rows: (d.availability ?? []).map(a => [a.reference, a.service, a.percent, a.target, a.downtimeHours]) },
  { title: 'Connaissances', header: ['Indicateur', 'Valeur'], rows: [
    ['Articles publiés', d.knowledge.published], ['Publiés sur la période', d.knowledge.publishedInPeriod],
    ['Revues en retard', d.knowledge.reviewOverdue], ['Brouillons', d.knowledge.drafts],
    ['Articles à jour (%)', d.knowledge.upToDate ?? ''], ['Incidents résolus avec un article (%)', d.knowledge.incidentsWithArticle ?? '']] },
  { title: 'Articles les plus utilisés', header: ['Référence', 'Titre', 'Utilisations'], rows: (d.knowledge.topUsed ?? []).map(a => [a.reference, a.title, a.uses]) },
]
