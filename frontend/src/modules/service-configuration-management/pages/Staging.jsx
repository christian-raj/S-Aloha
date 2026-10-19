import React, { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '../../../api'
import { dateTimeFr } from '../../../core/fields'
import { SOURCE_LABELS } from '../config'
import { ScoreMeter, Sources, StagingBadge } from '../components/Discovery'

/** Vues du sas : chaque tuile filtre la liste ; « À traiter » réunit ce qui attend une décision. */
const VIEWS = [
  { id: 'open', label: 'À traiter', count: s => s.toReconcile + s.conflicts, tone: 'blue', hint: 'À rapprocher ou en conflit' },
  { id: 'conflict', label: 'En conflit', count: s => s.conflicts, tone: 'red', hint: 'Critères contradictoires', status: 'En conflit' },
  { id: 'unknown', label: 'Actifs inconnus', count: s => s.unknown, tone: 'amber', hint: 'Ni dans l’annuaire ni en CMDB (Shadow IT)' },
  { id: 'reconciled', label: 'Rapprochés', count: s => s.reconciled, tone: 'emerald', hint: 'Reliés à un CI', status: 'Rapproché' },
  { id: 'ignored', label: 'Ignorés', count: s => s.ignored, tone: 'neutral', hint: 'Exclus de la CMDB', status: 'Ignoré' },
  { id: 'relations', label: 'Relations proposées', count: s => s.relations ?? 0, tone: 'violet', hint: 'Hyperviseur → machine, à valider' },
]

/** Un côté d'une relation proposée : l'actif du sas et le CI dont il est rapproché. */
function Side({ asset }) {
  return (
    <>
      <Link to={'/configuration/staging/' + asset.id}><b>{asset.hostname ?? asset.ipAddress}</b></Link>
      <div className="small">{asset.ci
        ? <Link to={'/configuration/' + asset.ci.id}>{asset.ci.reference} {asset.ci.title}</Link>
        : <span className="muted">non rapproché ({asset.status})</span>}</div>
    </>
  )
}

/**
 * CFG-55 : relations déduites par la découverte, proposées au gestionnaire. Accepter crée la
 * relation dans la CMDB, une fois les deux actifs rapprochés ; écarter ne la repropose plus.
 */
function Relations({ onChanged }) {
  const [rows, setRows] = useState(null)
  const [error, setError] = useState('')
  const load = () => api.discovery.relations.list({ status: 'Proposée' }).then(setRows).catch(e => setError(e.message))
  useEffect(() => { load() }, [])
  const act = (fn, r) => fn(r.id).then(() => { setError(''); load(); onChanged() }).catch(e => setError(e.message))
  return (
    <>
      {error && <div className="error" role="alert">{error}</div>}
      <div className="table-scroll">
        <table>
          <thead><tr><th>Source</th><th>Relation</th><th>Cible</th><th>Motif</th><th></th></tr></thead>
          <tbody>
            {rows?.map(r => (
              <tr key={r.id}>
                <td><Side asset={r.source} /></td>
                <td><span className="badge t-progress">{r.type}</span></td>
                <td><Side asset={r.target} /></td>
                <td className="small">{r.reason}<div className="muted">vue le {dateTimeFr(r.lastSeenAt)}</div></td>
                <td className="row-actions">
                  <button className="btn small" onClick={() => act(api.discovery.relations.accept, r)} disabled={!r.ready}
                    title={r.ready ? undefined : 'Rapprocher d’abord les deux actifs de leurs CI'}>Accepter</button>
                  <button className="btn ghost small" onClick={() => act(api.discovery.relations.dismiss, r)}>Écarter</button>
                </td>
              </tr>))}
            {rows?.length === 0 && <tr><td colSpan={5} className="muted">Aucune relation à valider.</td></tr>}
          </tbody>
        </table>
      </div>
      {!rows && !error && <p>Chargement…</p>}
    </>
  )
}
const OPEN = ['À rapprocher', 'En conflit']

/**
 * Sas de staging (CFG-40) : les actifs découverts attendent ici d'être rapprochés d'un CI,
 * créés en CI ou ignorés. Gestionnaires, sur leurs périmètres.
 */
export default function Staging() {
  const [params, setParams] = useSearchParams()
  const view = VIEWS.find(v => v.id === params.get('view')) ?? VIEWS[0]
  const [summary, setSummary] = useState(null)
  const [rows, setRows] = useState(null)
  const [q, setQ] = useState('')
  const [source, setSource] = useState('')
  const [error, setError] = useState('')
  const nav = useNavigate()

  const load = () => {
    api.discovery.staging.summary().then(setSummary).catch(e => setError(e.message))
    if (view.id === 'relations') return
    api.discovery.staging.list({
      ...(view.status && { status: view.status }), ...(view.id === 'unknown' && { unknown: true }),
      ...(source && { source }), ...(q && { q })
    }).then(list => setRows(view.id === 'open' ? list.filter(a => OPEN.includes(a.status)) : list)).catch(e => setError(e.message))
  }
  useEffect(() => { load() }, [view.id, source])

  const empty = summary && Object.values(summary).every(n => n === 0)
  return (
    <>
      <h1 className="page-title">Sas de staging</h1>
      <p className="page-sub">
        Les équipements découverts sur le réseau et dans l’annuaire attendent ici votre décision :
        aucun n’entre dans la CMDB sans être rapproché d’un CI ou créé (CFG-40).
      </p>
      {error && <div className="error">{error}</div>}

      {empty ? (
        <div className="card empty-state">
          <h3>Aucun actif découvert pour l’instant</h3>
          <p className="muted">Créez une tâche de scan (balayage IP, SNMP ou annuaire) et lancez-la : les équipements trouvés arriveront ici.</p>
          <Link className="btn" to="/settings/discovery">Paramétrer la découverte</Link>
        </div>
      ) : <>
        <div className="filter-tiles" role="tablist" aria-label="Vues du sas">
          {VIEWS.map(v => (
            <button key={v.id} type="button" role="tab" aria-selected={v.id === view.id}
              className={`filter-tile tone-${v.tone}${v.id === view.id ? ' on' : ''}`}
              onClick={() => setParams(v.id === 'open' ? {} : { view: v.id }, { replace: true })}>
              <span className="n">{summary ? v.count(summary) : '…'}</span>
              <span className="l">{v.label}</span>
              <span className="h">{v.hint}</span>
            </button>
          ))}
        </div>

        {view.id === 'relations' ? <Relations onChanged={load} /> : <>
        <div className="toolbar">
          <input style={{ maxWidth: 280 }} placeholder="IP, nom d’hôte, MAC, n° de série…" value={q}
            onChange={e => setQ(e.target.value)} onKeyDown={e => e.key === 'Enter' && load()} aria-label="Rechercher un actif" />
          <select style={{ maxWidth: 200 }} value={source} onChange={e => setSource(e.target.value)} aria-label="Source">
            <option value="">Toutes les sources</option>
            {Object.entries(SOURCE_LABELS).map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select>
          <Link className="btn ghost" style={{ marginLeft: 'auto' }} to="/settings/discovery">Tâches de scan</Link>
        </div>

        <div className="table-scroll">
          <table>
            <thead><tr>
              <th>Statut</th><th>Équipement</th><th>Adresse</th><th>Sources</th><th>Rapprochement</th><th>Vu le</th>
            </tr></thead>
            <tbody>
              {rows?.map(a => (
                <tr key={a.id} className="clickable" onClick={() => nav('/configuration/staging/' + a.id)}>
                  <td><StagingBadge status={a.status} />{a.unknown && <span className="badge t-wait" style={{ marginLeft: 6 }}>Inconnu</span>}</td>
                  <td><b>{a.hostname ?? a.ipAddress}</b><div className="muted small">{a.detectedType}{a.detectedOs && ` · ${a.detectedOs}`}</div></td>
                  <td className="mono small">{a.ipAddress ?? '—'}{a.macAddress && <div className="muted">{a.macAddress}</div>}</td>
                  <td className="small"><Sources value={a.sources} /></td>
                  <td>
                    <ScoreMeter score={a.confidenceScore} rule={a.matchRule} />
                    {a.matchedCi && <div className="small">{a.status === 'Rapproché' ? '→ ' : 'proposé : '}<b>{a.matchedCi.reference}</b> {a.matchedCi.title}</div>}
                  </td>
                  <td className="small">{dateTimeFr(a.lastSeenAt)}</td>
                </tr>
              ))}
              {rows?.length === 0 && <tr><td colSpan={6} className="muted">Rien dans cette vue : tout est à jour.</td></tr>}
            </tbody>
          </table>
        </div>
        {!rows && !error && <p>Chargement…</p>}
        </>}
      </>}
    </>
  )
}
