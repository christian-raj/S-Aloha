import React, { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getUser } from '../../api'
import RecordForm, { toForm, toPayload } from './RecordForm'
import StatusBadge from './StatusBadge'

const isManagerRole = () => ['Admin', 'Manager'].includes(getUser()?.role)

/** Formulaire de création : champs de la configuration, statut initial si le module le permet. */
function NewRecord({ config, onCreated }) {
  const [f, setF] = useState(() => ({ ...toForm(config.fields), status: '' }))
  const [error, setError] = useState('')
  const merge = patch => setF(prev => ({ ...prev, ...patch }))
  const submit = () => config.api.create({ ...toPayload(config.fields, f), status: f.status || null })
    .then(onCreated).catch(e => setError(e.message))
  const missing = config.fields.some(fd => fd.required && fd.create !== false && fd.type !== 'checkbox' && !f[fd.key])
  return (
    <div className="card" style={{ marginBottom: 20 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 14 }}>{config.createTitle}</h3>
      {error && <div className="error">{error}</div>}
      {config.statusAtCreation && (
        <div className="field" style={{ maxWidth: 240 }}><label>Statut</label>
          <select value={f.status} onChange={e => merge({ status: e.target.value })}>
            <option value="">{config.statusAtCreation}</option>
            {config.statuses.map(s => <option key={s.value}>{s.value}</option>)}
          </select></div>
      )}
      <RecordForm fields={config.fields} form={f} onChange={merge} creating />
      <button className="btn" onClick={submit} disabled={missing}>Enregistrer</button>
    </div>
  )
}

/**
 * Registre d'un processus : recherche, filtre de statut (et filtre propre au
 * module), « mes éléments », création, tableau cliquable vers le détail.
 */
/** `intro` : contenu placé sous le titre de la page (ex. « Chercher d'abord » des connaissances). */
export default function RecordList({ config, intro }) {
  const [items, setItems] = useState([])
  const [status, setStatus] = useState('')
  const [extra, setExtra] = useState('')
  const [mine, setMine] = useState(false)
  const [q, setQ] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [error, setError] = useState('')
  const nav = useNavigate()
  const canCreate = !config.managerOnly || isManagerRole()

  const load = () => config.api.list({
    ...(status && { status }), ...(q && { q }), ...(mine && { owner: 'me' }),
    ...(extra && { [config.filter.key]: extra })
  }).then(setItems).catch(e => setError(e.message))
  useEffect(() => { load() }, [status, mine, extra])

  return (
    <>
      <h1 className="page-title">{config.title}</h1>
      <p className="page-sub">{config.sub}</p>
      {intro}
      {error && <div className="error">{error}</div>}

      <div style={{ display: 'flex', gap: 10, marginBottom: 16, flexWrap: 'wrap', alignItems: 'center' }}>
        <input style={{ maxWidth: 260 }} placeholder={config.searchPlaceholder ?? 'Rechercher (titre, référence)…'}
          value={q} onChange={e => setQ(e.target.value)} onKeyDown={e => e.key === 'Enter' && load()} />
        <select style={{ maxWidth: 180 }} value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Tous les statuts</option>
          {config.statuses.map(s => <option key={s.value}>{s.value}</option>)}
        </select>
        {config.filter && (
          <select style={{ maxWidth: 200 }} value={extra} onChange={e => setExtra(e.target.value)}>
            <option value="">{config.filter.label}</option>
            {config.filter.options.map(o => <option key={o}>{o}</option>)}
          </select>
        )}
        {config.ownerLabel && (
          <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 14 }}>
            <input type="checkbox" checked={mine} onChange={e => setMine(e.target.checked)} style={{ width: 'auto' }} />
            {config.ownerLabel}
          </label>
        )}
        {canCreate && (
          <button className="btn" style={{ marginLeft: 'auto' }} onClick={() => setShowForm(v => !v)}>
            {showForm ? 'Fermer' : config.createLabel}
          </button>
        )}
      </div>

      {showForm && <NewRecord config={config} onCreated={r => nav(`${config.basePath}/${r.id}`)} />}

      <table>
        <thead><tr>
          <th>Référence</th><th>{config.titleLabel ?? 'Titre'}</th>
          {config.columns.map(c => <th key={c.h}>{c.h}</th>)}<th>Statut</th>
        </tr></thead>
        <tbody>
          {items.map(r => (
            <tr key={r.id} className="clickable" onClick={() => nav(`${config.basePath}/${r.id}`)}>
              <td><b>{r.reference}</b></td>
              <td>{r.title}</td>
              {config.columns.map(c => <td key={c.h}>{c.v(r)}</td>)}
              <td><StatusBadge statuses={config.statuses} status={r.status} /></td>
            </tr>
          ))}
          {items.length === 0 && <tr><td colSpan={config.columns.length + 3} style={{ color: 'var(--muted)' }}>
            {config.empty}</td></tr>}
        </tbody>
      </table>
    </>
  )
}
