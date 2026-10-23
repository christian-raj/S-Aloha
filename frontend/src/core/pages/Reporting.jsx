import React, { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../api'
import usePerimeters, { perimeterName } from '../usePerimeters'
import Overview, { overviewCsv } from './reporting/Overview'
import IncidentsTab, { incidentsCsv } from './reporting/IncidentsTab'
import ProblemsTab, { problemsCsv } from './reporting/ProblemsTab'
import RequestsTab, { requestsCsv } from './reporting/RequestsTab'
import ChangesProjectsTab, { changesProjectsCsv } from './reporting/ChangesProjectsTab'
import ComplianceTab, { complianceCsv } from './reporting/ComplianceTab'
import ConfigurationTab, { configurationCsv } from './reporting/ConfigurationTab'
import { date, downloadCsv } from './reporting/exportCsv'

/**
 * Onglets du tableau de bord (REP-xx, docs/reference/reporting.md) :
 * `badge` — clé des alertes renvoyées par la synthèse (REP-07) ;
 * `service` — le filtre de service s'applique-t-il à l'onglet.
 */
export const TABS = [
  { id: 'overview', label: 'Synthèse globale', load: 'overview', View: Overview, csv: overviewCsv, service: true },
  { id: 'incidents', label: 'Incidents', load: 'incidents', View: IncidentsTab, csv: incidentsCsv, badge: 'incidents', service: true },
  { id: 'requests', label: 'Demandes', load: 'requests', View: RequestsTab, csv: requestsCsv, badge: 'requests', service: true },
  { id: 'problems', label: 'Problèmes', load: 'problems', View: ProblemsTab, csv: problemsCsv, badge: 'problems', service: true },
  { id: 'changes', label: 'Changements & projets', load: 'changes-projects', View: ChangesProjectsTab, csv: changesProjectsCsv, badge: 'changesProjects', service: false },
  { id: 'compliance', label: 'Conformité & métriques', load: 'compliance', View: ComplianceTab, csv: complianceCsv, badge: 'compliance', service: true },
  { id: 'configuration', label: 'Configuration & inventaire', load: 'configuration', View: ConfigurationTab, csv: configurationCsv, badge: 'configuration', service: false },
]

export const PERIODS = [
  { id: '7', label: '7 derniers jours' },
  { id: '30', label: '30 derniers jours' },
  { id: '90', label: '90 derniers jours' },
  { id: '365', label: '12 derniers mois' },
  { id: 'custom', label: 'Personnalisée' },
]

const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** Bornes de la période choisie, au format attendu par l'API (bornes incluses). */
export function periodBounds(period, from, to, today = new Date()) {
  if (period === 'custom' && from && to) return { from, to }
  const start = new Date(today); start.setDate(start.getDate() - (Number(period) || 30) + 1)
  return { from: iso(start), to: iso(today) }
}

export default function Reporting() {
  const [params, setParams] = useSearchParams()
  const tab = TABS.find(t => t.id === params.get('tab')) ?? TABS[0]
  const period = params.get('period') ?? '30'
  const service = params.get('service') ?? ''
  const perimeter = params.get('perimeter') ?? ''
  const perimeters = usePerimeters()
  const bounds = periodBounds(period, params.get('from'), params.get('to'))
  // PER-09 : les indicateurs portent sur mes périmètres ; on peut n'en retenir qu'un.
  const query = useMemo(() => ({ ...bounds, ...(service ? { service } : {}), ...(perimeter ? { perimeter } : {}) }),
    [bounds.from, bounds.to, service, perimeter])

  const [services, setServices] = useState([])
  const [overview, setOverview] = useState(null)
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [printing, setPrinting] = useState(null)   // données de tous les onglets, le temps de l'impression

  const set = (changes) => setParams(p => {
    const next = new URLSearchParams(p)
    Object.entries(changes).forEach(([k, v]) => (v ? next.set(k, v) : next.delete(k)))
    return next
  }, { replace: true })

  useEffect(() => { api.services.list().then(l => setServices(l.map(s => s.title).sort((a, b) => a.localeCompare(b, 'fr')))).catch(() => {}) }, [])
  // La synthèse alimente aussi les badges des onglets : chargée quel que soit l'onglet ouvert.
  useEffect(() => { api.reports.dashboard('overview', query).then(setOverview).catch(e => setError(e.message)) }, [query])
  useEffect(() => {
    setError('')
    if (tab.id === 'overview') return
    api.reports.dashboard(tab.load, query).then(value => setData({ tab: tab.id, value })).catch(e => setError(e.message))
  }, [tab.id, query])

  // Export PDF (REP-08) : tous les onglets, imprimés par le navigateur.
  const exportPdf = async () => {
    try {
      const all = await Promise.all(TABS.map(t => t.id === 'overview' ? overview : api.reports.dashboard(t.load, query)))
      setPrinting(all)
    } catch (e) { setError(e.message) }
  }
  useEffect(() => {
    if (!printing) return
    const done = () => setPrinting(null)
    window.addEventListener('afterprint', done, { once: true })
    const timer = setTimeout(() => window.print(), 300)   // le temps du rendu des graphiques
    return () => { clearTimeout(timer); window.removeEventListener('afterprint', done) }
  }, [printing])

  // Données rattachées à l'onglet qui les a demandées : au changement d'onglet,
  // la vue ne reçoit jamais celles de l'onglet précédent.
  const current = tab.id === 'overview' ? overview : data?.tab === tab.id ? data.value : null
  const exportCsv = () => downloadCsv(`s-aloha-${tab.id}-${bounds.from}-${bounds.to}.csv`, [
    { title: `S-Aloha — ${tab.label}`, header: ['Période', 'Service', 'Périmètre'], rows: [[`${date(bounds.from)} – ${date(bounds.to)}`, tab.service ? service || 'Tous' : 'Sans objet',
      perimeter ? perimeterName(perimeters, Number(perimeter)) : 'Tous mes périmètres']] },
    ...tab.csv(current)])
  const badges = overview?.badges ?? {}

  return (
    <>
      <h1 className="page-title">Reporting</h1>
      <p className="page-sub">Tableau de bord des pratiques : santé des services, incidents, problèmes, changements et projets, conformité, configuration.</p>

      <div className="report-toolbar no-print" role="search" aria-label="Filtres du tableau de bord">
        <label>Période
          <select value={period} onChange={e => set(e.target.value === 'custom'
            ? { period: 'custom', from: bounds.from, to: bounds.to } : { period: e.target.value, from: null, to: null })}>
            {PERIODS.map(p => <option key={p.id} value={p.id}>{p.label}</option>)}
          </select>
        </label>
        {period === 'custom' && <>
          <label>Du <input type="date" value={bounds.from} max={bounds.to} onChange={e => e.target.value && set({ from: e.target.value })} /></label>
          <label>Au <input type="date" value={bounds.to} min={bounds.from} onChange={e => e.target.value && set({ to: e.target.value })} /></label>
        </>}
        <label>Service
          <select value={service} onChange={e => set({ service: e.target.value })}>
            <option value="">Tous les services</option>
            {services.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>
        {perimeters.length > 1 && <label>Périmètre
          <select value={perimeter} onChange={e => set({ perimeter: e.target.value })}>
            <option value="">Tous mes périmètres</option>
            {perimeters.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </label>}
        <div className="report-export">
          <button className="btn ghost small" onClick={exportCsv} disabled={!current}>Exporter l'onglet (CSV / Excel)</button>
          <button className="btn small" onClick={exportPdf} disabled={!overview}>Rapport complet (PDF)</button>
        </div>
      </div>

      <div className="tabs report-tabs no-print" role="tablist">
        {TABS.map(t => {
          const b = t.badge && badges[t.badge]
          return (
            <button key={t.id} role="tab" aria-selected={t.id === tab.id} className={t.id === tab.id ? 'on' : ''}
              onClick={() => set({ tab: t.id === 'overview' ? null : t.id })}>
              {t.label}
              {b?.count > 0 && <span className={`tab-badge ${b.tone}`} aria-label={`${b.count} alerte(s)`}>{b.count}</span>}
            </button>)
        })}
      </div>

      <p className="print-only report-print-head">
        Période du {date(bounds.from)} au {date(bounds.to)} — service : {service || 'tous'} — édité le {new Date().toLocaleString('fr-FR')}
      </p>
      {service && !tab.service && !printing &&
        <p className="notice small no-print">{tab.id === 'configuration'
          ? 'Le filtre de service ne s\'applique pas à la configuration : la CMDB est montrée en entier.'
          : 'Le filtre de service ne s\'applique pas aux changements ni aux projets, qui ne portent pas de service.'}</p>}
      {error && <div className="error">{error}</div>}

      {printing ? TABS.map((t, i) => (
        <section key={t.id} className="report-section">
          <h2 className="report-section-title">{t.label}</h2>
          <t.View data={printing[i]} />
        </section>
      )) : current ? <div role="tabpanel" aria-label={tab.label}><tab.View data={current} /></div>
        : !error && <p>Chargement…</p>}
    </>
  )
}
