import React, { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../../../api'
import { dateFr } from '../../../core/fields'
import { Kpi } from '../../../core/pages/reporting/charts'
import { downloadCsv, date } from '../../../core/pages/reporting/exportCsv'
import { CRITICALITIES, SUPPORT_TONES, WARRANTY_TONES } from '../config'

const pct = (part, total) => (total ? Math.round((100 * part) / total) : null)
const yes = v => (v ? 'Oui' : 'Non')
const Tone = ({ map, value }) => <span className={'badge ' + (map[value] ?? 't-closed')}>{value}</span>
const match = (text, q) => !q || text.some(t => (t ?? '').toLowerCase().includes(q.toLowerCase()))

/** Portefeuille applicatif : gouvernance, criticité, cycle de vie, exigences RGPD et NIS 2 (CFG-43). */
function Applications({ rows }) {
  return (
    <div className="table-scroll">
      <table className="compact">
        <thead><tr>
          <th>Code</th><th>Application</th><th>Direction</th><th>Propriétaire métier</th><th>Responsable DSI</th>
          <th>Criticité</th><th>Cycle</th><th>RTO / RPO</th><th>RGPD</th><th>NIS 2</th>
        </tr></thead>
        <tbody>
          {rows.map(a => (
            <tr key={a.id} className={a.governed ? undefined : 'warn-row'}>
              <td className="mono">{a.appCode ?? '—'}</td>
              <td><Link to={'/configuration/' + a.id}><b>{a.reference}</b> {a.title}</Link><div className="muted small">{a.status} · {a.environment}</div></td>
              <td>{a.businessUnit ?? '—'}</td>
              <td>{a.businessOwnerDisplayName ?? '—'}</td>
              <td>{a.technicalOwnerDisplayName ?? '—'}</td>
              <td>{a.criticality ? <span className={'badge crit-' + CRITICALITIES.indexOf(a.criticality)}>{a.criticality}</span> : '—'}</td>
              <td>{a.appLifecycle ?? '—'}</td>
              <td className="mono">{a.rtoHours ?? '—'} h / {a.rpoHours ?? '—'} h</td>
              <td>{yes(a.gdprSensitive)}</td>
              <td>{yes(a.nis2Scope)}</td>
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan={10} className="muted">Aucune application.</td></tr>}
        </tbody>
      </table>
    </div>
  )
}

/** Actifs matériels : identification, garantie et fin de support (CFG-44, CFG-47). */
function Hardware({ rows }) {
  return (
    <div className="table-scroll">
      <table className="compact">
        <thead><tr>
          <th>Équipement</th><th>Type</th><th>Constructeur / modèle</th><th>N° de série</th><th>Emplacement</th>
          <th>Système</th><th>Garantie</th><th>Fin de support</th>
        </tr></thead>
        <tbody>
          {rows.map(h => (
            <tr key={h.id}>
              <td><Link to={'/configuration/' + h.id}><b>{h.reference}</b> {h.title}</Link><div className="muted small">{h.status}{h.ipAddress && ` · ${h.ipAddress}`}</div></td>
              <td>{h.ciType}</td>
              <td>{[h.vendor, h.model].filter(Boolean).join(' ') || '—'}</td>
              <td className="mono">{h.serialNumber ?? '—'}</td>
              <td>{h.location || '—'}</td>
              <td className="small">{h.operatingSystem ?? '—'}</td>
              <td><Tone map={WARRANTY_TONES} value={h.warranty} />{h.warrantyExpiry && <div className="muted small">{dateFr(h.warrantyExpiry)}</div>}</td>
              <td><Tone map={SUPPORT_TONES} value={h.supportState} />
                {h.supportDate && <div className="muted small">{dateFr(h.supportDate)}{h.supportSource !== 'Fiche du CI' && ` · ${h.supportSource}`}</div>}</td>
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan={8} className="muted">Aucun actif matériel.</td></tr>}
        </tbody>
      </table>
    </div>
  )
}

const TABS = [
  { id: 'applications', label: 'Portefeuille applicatif' },
  { id: 'hardware', label: 'Actifs matériels' },
]

/** Rapport d'inventaire (CFG-39) : portefeuille applicatif et actifs matériels, export CSV et impression. */
export default function Inventory() {
  const [params, setParams] = useSearchParams()
  const tab = TABS.find(t => t.id === params.get('tab')) ?? TABS[0]
  const [data, setData] = useState(null)
  const [retired, setRetired] = useState(false)
  const [q, setQ] = useState('')
  const [only, setOnly] = useState('')
  const [error, setError] = useState('')
  useEffect(() => { api.configurationItems.inventory(retired).then(setData).catch(e => setError(e.message)) }, [retired])

  const apps = useMemo(() => (data?.applications ?? []).filter(a =>
    match([a.title, a.reference, a.appCode, a.businessUnit, a.businessOwnerDisplayName, a.technicalOwnerDisplayName], q)
    && (!only || (only === 'ungoverned' ? !a.governed : a.criticality === only))), [data, q, only])
  const hardware = useMemo(() => (data?.hardware ?? []).filter(h =>
    match([h.title, h.reference, h.vendor, h.model, h.serialNumber, h.location, h.ipAddress, h.operatingSystem], q)
    && (!only || h.supportState === only || h.warranty === only)), [data, q, only])

  const exportCsv = () => downloadCsv(`s-aloha-inventaire-${tab.id}-${new Date().toISOString().slice(0, 10)}.csv`, tab.id === 'applications'
    ? [{ title: 'Portefeuille applicatif', header: ['Code', 'Référence', 'Application', 'Statut', 'Environnement', 'Direction', 'Propriétaire métier',
        'Responsable DSI', 'Criticité', 'Cycle', 'RTO (h)', 'RPO (h)', 'RGPD', 'NIS 2', 'Gouvernance complète'],
        rows: apps.map(a => [a.appCode, a.reference, a.title, a.status, a.environment, a.businessUnit, a.businessOwnerDisplayName,
          a.technicalOwnerDisplayName, a.criticality, a.appLifecycle, a.rtoHours, a.rpoHours, yes(a.gdprSensitive), yes(a.nis2Scope), yes(a.governed)]) }]
    : [{ title: 'Actifs matériels', header: ['Référence', 'Équipement', 'Type', 'Statut', 'Constructeur', 'Modèle', 'N° de série', 'Emplacement',
        'Adresse IP', 'Système', 'Acquisition', 'Fin de garantie', 'Garantie', 'Fin de support', 'État du support', 'Source de la date'],
        rows: hardware.map(h => [h.reference, h.title, h.ciType, h.status, h.vendor, h.model, h.serialNumber, h.location, h.ipAddress,
          h.operatingSystem, date(h.purchaseDate), date(h.warrantyExpiry), h.warranty, date(h.supportDate), h.supportState, h.supportSource]) }])

  const s = data?.summary
  return (
    <>
      <h1 className="page-title">Inventaire</h1>
      <p className="page-sub">Portefeuille applicatif et actifs matériels de vos périmètres, tenus dans la CMDB.</p>
      {error && <div className="error">{error}</div>}
      {s && (
        <div className="kpi-row">
          <Kpi value={s.applications} label="Applications"
            sub={s.applications ? `${pct(s.governed, s.applications)} % avec leur gouvernance complète` : null}
            alert={s.applications - s.governed > 0 ? `${s.applications - s.governed} à compléter` : null} />
          <Kpi value={s.hardware} label="Actifs matériels"
            sub={s.missingSerial ? `${s.missingSerial} équipement(s) sans numéro de série` : null} />
          <Kpi value={s.supportPassed} label="Fin de support dépassée" alert={s.supportPassed > 0 ? 'À remplacer ou isoler' : null}
            sub={s.supportNear ? `${s.supportNear} dans les 6 mois` : null} />
          <Kpi value={s.warrantyExpired} label="Garantie expirée" />
        </div>
      )}

      <div className="tabs no-print" role="tablist">
        {TABS.map(t => <button key={t.id} role="tab" aria-selected={t.id === tab.id} className={t.id === tab.id ? 'on' : ''}
          onClick={() => { setOnly(''); setParams(t.id === 'applications' ? {} : { tab: t.id }, { replace: true }) }}>{t.label}</button>)}
      </div>
      <div className="toolbar no-print">
        <input style={{ maxWidth: 280 }} placeholder="Rechercher…" value={q} onChange={e => setQ(e.target.value)} aria-label="Rechercher dans l’inventaire" />
        <select style={{ maxWidth: 240 }} value={only} onChange={e => setOnly(e.target.value)} aria-label="Filtrer">
          <option value="">Tout</option>
          {tab.id === 'applications'
            ? <><option value="ungoverned">Gouvernance incomplète</option>{CRITICALITIES.map(c => <option key={c} value={c}>Criticité {c.toLowerCase()}</option>)}</>
            : Object.keys(SUPPORT_TONES).concat('Expirée', 'Expire bientôt').map(v => <option key={v} value={v}>{v}</option>)}
        </select>
        <label className="check"><input type="checkbox" checked={retired} onChange={e => setRetired(e.target.checked)} /> Inclure les retirés</label>
        <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <button className="btn ghost small" onClick={exportCsv} disabled={!data}>Exporter (CSV / Excel)</button>
          <button className="btn small" onClick={() => window.print()} disabled={!data}>Imprimer</button>
        </span>
      </div>
      {!data && !error && <p>Chargement…</p>}
      {data && (tab.id === 'applications' ? <Applications rows={apps} /> : <Hardware rows={hardware} />)}
      <p className="muted small" style={{ marginTop: 12 }}>
        Fins de support : date de la fiche du CI, ou celle du référentiel <Link to="/settings/discovery?tab=eol">Fins de support</Link> dont
        le produit figure dans le système, le constructeur ou le modèle ; la plus proche l’emporte.
      </p>
    </>
  )
}
