import React from 'react'
import { Link } from 'react-router-dom'
import { HBars, Kpi, LineChart } from './charts'
import { date, dateTime, fmt } from './exportCsv'

const SOURCES = { IpSweep: 'Balayage IP', Snmp: 'SNMP', ActiveDirectory: 'Active Directory' }
const SUPPORT_TONE = { 'Fin de support dépassée': 't-bad', 'Fin de support proche': 't-wait' }

/** Onglet Configuration et inventaire (REP-50 à REP-53) : état de la CMDB à date, découverte de la période. */
export default function ConfigurationTab({ data }) {
  const { kpis, staging } = data
  return (
    <div className="report-body">
      <div className="kpi-row">
        <Kpi value={kpis.cis} label="CI en CMDB (non retirés)" sub={`${kpis.applications} application(s), ${kpis.hardware} actif(s) matériel(s)`} />
        <Kpi value={fmt(kpis.portfolioCoverage, 0)} suffix=" %" label="Portefeuille applicatif gouverné"
          sub="Code, direction, propriétaire métier, responsable DSI, criticité" />
        <Kpi value={kpis.supportPassed} label="Actifs en fin de support"
          alert={kpis.supportPassed > 0 ? 'Alerte : obsolescence (NIS 2)' : null}
          sub={kpis.supportNear ? `${kpis.supportNear} dans les 6 mois` : null} />
        {staging
          ? <Kpi value={staging.toReconcile + staging.conflicts} label="Actifs à traiter dans le sas"
              alert={staging.unknown > 0 ? `${staging.unknown} actif(s) inconnu(s)` : null}
              sub={<Link to="/configuration/staging">Ouvrir le sas de staging</Link>} />
          : <Kpi value={kpis.supportNear} label="Fins de support dans les 6 mois" />}
        {/* CFG-32, CFG-34 */}
        <Kpi value={fmt(kpis.accuracy, 0)} suffix=" %" label="Exactitude de la CMDB" sub={`CI en service vérifiés depuis moins de 12 mois, sur ${kpis.accuracyMeasured ?? 0} en service`} />
        <Kpi value={kpis.orphans} label="CI sans propriétaire ni relation" />
      </div>

      {/* CFG-33 */}
      {(data.topIncidentCis ?? []).length > 0 && <HBars title="CI les plus impliqués dans les incidents de la période"
        data={data.topIncidentCis.map(c => ({ label: `${c.reference} ${c.title}`, count: c.incidents }))} />}

      <div className="report-grid">
        <HBars title="CI par type" data={data.byType} empty="Aucun CI." />
        <HBars title="CI par environnement" data={data.byEnvironment} empty="Aucun CI." />
        <HBars title="CI par statut" data={data.byStatus} empty="Aucun CI." />
        <HBars title="Applications par criticité" data={data.byCriticality} empty="Aucune application." />
        <HBars title="Support des actifs matériels" data={data.support} empty="Aucun actif matériel."
          note="Date de la fiche du CI ou du référentiel des fins de support ; « proche » : moins de 6 mois." />
        {staging && <HBars title="Actifs découverts par source" data={staging.bySource.map(s => ({ ...s, label: SOURCES[s.label] ?? s.label }))}
          empty="Aucun actif découvert." />}
      </div>

      {staging && (
        <LineChart title="Découverte, par jour" days={staging.discovered} series={[
          { key: 'discovered', label: 'Nouveaux actifs découverts', short: 'nouv.', slot: 1 },
          { key: 'reconciled', label: 'Actifs rapprochés', short: 'rapp.', slot: 2 },
        ]} />
      )}

      <div className="report-grid wide">
        <div className="card">
          <h3 className="card-title">Actifs en fin de support</h3>
          {data.obsolete.length === 0 ? <p className="muted small">Aucun actif en fin de support, ni dans les 6 mois.</p> : (
            <div className="table-scroll">
              <table className="compact">
                <thead><tr><th>CI</th><th>Type</th><th>Fin de support</th><th>Source</th></tr></thead>
                <tbody>{data.obsolete.map(o => (
                  <tr key={o.id}>
                    <td><Link to={'/configuration/' + o.id}>{o.reference}</Link> {o.title}</td><td>{o.ciType}</td>
                    <td><span className={'badge ' + (SUPPORT_TONE[o.state] ?? 't-closed')}>{date(o.date)}</span></td>
                    <td className="small">{o.source}</td>
                  </tr>))}</tbody>
              </table>
            </div>
          )}
        </div>
        {staging && (
          <div className="card">
            <h3 className="card-title">Actifs inconnus (Shadow IT)</h3>
            <p className="muted small" style={{ marginBottom: 8 }}>Vus sur le réseau, absents de l’annuaire et sans CI rapproché.</p>
            {staging.unknownAssets.length === 0 ? <p className="muted small">Aucun.</p> : (
              <div className="table-scroll">
                <table className="compact">
                  <thead><tr><th>Équipement</th><th>Type</th><th>Détecté le</th></tr></thead>
                  <tbody>{staging.unknownAssets.map(a => (
                    <tr key={a.id}>
                      <td><Link to={'/configuration/staging/' + a.id}>{a.hostname ?? a.ipAddress}</Link>{a.hostname && <span className="muted small"> {a.ipAddress}</span>}</td>
                      <td>{a.detectedType}</td><td className="small">{dateTime(a.firstSeenAt)}</td>
                    </tr>))}</tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

const bars = (title, rows) => ({ title, header: ['Valeur', 'Nombre'], rows: rows.map(r => [r.label, r.count]) })

export const configurationCsv = (d) => [
  { title: 'Indicateurs clés', header: ['Indicateur', 'Valeur'], rows: [
    ['CI non retirés', d.kpis.cis], ['Applications', d.kpis.applications],
    ['Portefeuille applicatif gouverné (%)', d.kpis.portfolioCoverage], ['Actifs matériels', d.kpis.hardware],
    ['Fin de support dépassée', d.kpis.supportPassed], ['Fin de support dans les 6 mois', d.kpis.supportNear],
    ['Exactitude de la CMDB (%)', d.kpis.accuracy ?? ''], ['CI en service mesurés', d.kpis.accuracyMeasured ?? ''], ['CI sans propriétaire ni relation', d.kpis.orphans ?? ''],
    ...(d.staging ? [['Actifs à rapprocher', d.staging.toReconcile], ['Actifs en conflit', d.staging.conflicts],
      ['Actifs inconnus', d.staging.unknown]] : [])] },
  bars('CI par type', d.byType), bars('CI par environnement', d.byEnvironment), bars('CI par statut', d.byStatus),
  bars('Applications par criticité', d.byCriticality), bars('Support des actifs matériels', d.support),
  { title: 'CI les plus impliqués dans les incidents', header: ['Référence', 'CI', 'Type', 'Incidents'], rows: (d.topIncidentCis ?? []).map(c => [c.reference, c.title, c.ciType, c.incidents]) },
  { title: 'CI sans propriétaire ni relation', header: ['Référence', 'CI', 'Type', 'Statut'], rows: (d.orphanCis ?? []).map(c => [c.reference, c.title, c.ciType, c.status]) },
  { title: 'Actifs en fin de support', header: ['Référence', 'CI', 'Type', 'État', 'Date', 'Source'],
    rows: d.obsolete.map(o => [o.reference, o.title, o.ciType, o.state, date(o.date), o.source]) },
  ...(d.staging ? [
    { title: 'Découverte par jour', header: ['Jour', 'Nouveaux actifs', 'Rapprochés'],
      rows: d.staging.discovered.map(x => [date(x.date), x.discovered, x.reconciled]) },
    { title: 'Actifs inconnus', header: ['Nom d’hôte', 'Adresse IP', 'Type', 'Sources', 'Détecté le'],
      rows: d.staging.unknownAssets.map(a => [a.hostname, a.ipAddress, a.detectedType, a.sources, dateTime(a.firstSeenAt)]) },
  ] : []),
]
