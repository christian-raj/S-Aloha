import React, { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { getUser } from '../../api'
import RecordForm, { toForm, toPayload } from './RecordForm'
import StatusBadge from './StatusBadge'
import LinkedItems from './LinkedItems'
import { dateFr } from '../fields'

/** Édition : statut + champs ; recréé (clé) à chaque rechargement de l'enregistrement. */
function Editor({ config, record, onSaved }) {
  const role = getUser()?.role
  const isManager = ['Admin', 'Manager'].includes(role)
  const readOnly = config.managerOnly && !isManager
  const [f, setF] = useState(() => ({ ...toForm(config.fields, record), status: record.status }))
  const [msg, setMsg] = useState(null)
  const nav = useNavigate()
  const merge = patch => setF(prev => ({ ...prev, ...patch }))

  const save = () => config.api.update(record.id, { ...toPayload(config.fields, f), status: f.status })
    .then(saved => {
      // L'API peut ramener le statut en arrière (article retouché, changement modifié).
      setMsg({ ok: true, text: saved.status === f.status ? 'Modifications enregistrées.'
        : `Modifications enregistrées. Statut repassé à « ${saved.status} » : une nouvelle validation d'un gestionnaire est requise.` })
      onSaved()
    })
    .catch(e => setMsg({ ok: false, text: e.message }))
  const remove = () => window.confirm(`Supprimer définitivement ${record.reference} ?`)
    && config.api.remove(record.id).then(() => nav(config.basePath)).catch(e => setMsg({ ok: false, text: e.message }))

  return (
    <div className="card">
      {msg && <div className="error" style={msg.ok ? { background: 'var(--blue-soft)', color: 'var(--navy)' } : undefined}>{msg.text}</div>}
      <div className="field" style={{ maxWidth: 260 }}>
        <label>Statut</label>
        <select value={f.status} onChange={e => merge({ status: e.target.value })} disabled={readOnly}>
          {config.statuses.map(s => (
            <option key={s.value} value={s.value}
              disabled={s.manager && !isManager && s.value !== record.status}>
              {s.value}{s.manager ? ' (gestionnaire)' : ''}
            </option>))}
        </select>
      </div>
      <RecordForm fields={config.fields} form={f} onChange={merge} disabled={readOnly} />
      <div style={{ display: 'flex', gap: 10 }}>
        {!readOnly && <button className="btn" onClick={save} disabled={!f.title}>Enregistrer</button>}
        {role === 'Admin' && <button className="btn danger" style={{ marginLeft: 'auto' }} onClick={remove}>Supprimer</button>}
      </div>
    </div>
  )
}

/**
 * Fiche d'un enregistrement : en-tête (référence, statut, badges du module),
 * puis les onglets communs, dans l'ordre de docs/reference/frontend.md
 * § Navigation cible : Informations → onglet propre à la pratique → Liens.
 *
 * - `children(record, reload)` : complément de l'onglet Informations (action
 *   liée à l'enregistrement, ex. « Ouvrir un problème lié ») ;
 * - `tab = { label, render(record, reload) }` : l'onglet propre à la pratique
 *   (relations et impact d'un CI, accords d'un service, conflits d'un changement).
 */
export default function RecordDetail({ config, children, tab }) {
  const { id } = useParams()
  const [r, setR] = useState(null)
  const [error, setError] = useState('')
  const [version, setVersion] = useState(0)
  const [current, setCurrent] = useState('infos')
  const load = () => config.api.get(id).then(x => { setR(x); setVersion(v => v + 1) }).catch(e => setError(e.message))
  // Une autre fiche (lien suivi depuis l'onglet Liens) s'ouvre sur ses Informations.
  useEffect(() => { setCurrent('infos'); load() }, [id])
  if (error) return <div className="error">{error}</div>
  if (!r) return <p>Chargement…</p>

  const meta = config.meta?.(r).filter(Boolean) ?? []
  const tabs = [['infos', 'Informations'], tab && ['practice', tab.label], config.linkType && ['links', 'Liens']]
    .filter(Boolean)
  return (
    <>
      <p style={{ marginBottom: 8 }}><Link to={config.basePath}>← {config.title}</Link></p>
      <h1 className="page-title">{r.reference} — {r.title}</h1>
      <p className="page-sub">
        <StatusBadge statuses={config.statuses} status={r.status} />{' '}
        {config.badges?.(r)}{' '}
        Créé par {r.createdByDisplayName} le {dateFr(r.createdAt)}
        {meta.map(m => <React.Fragment key={m}> · {m}</React.Fragment>)}
      </p>
      {config.help && <p style={{ color: 'var(--muted)', fontSize: 13, marginTop: -16, marginBottom: 20 }}>{config.help}</p>}

      <div className="tabs" role="tablist">
        {tabs.map(([k, l]) => (
          <button key={k} role="tab" aria-selected={current === k}
            className={current === k ? 'on' : ''} onClick={() => setCurrent(k)}>{l}</button>))}
      </div>

      {current === 'infos' && <>
        <Editor key={version} config={config} record={r} onSaved={load} />
        {children?.(r, load)}
      </>}
      {current === 'practice' && tab.render(r, load)}
      {current === 'links' && <LinkedItems type={config.linkType} id={r.id} hint={config.linkHint} refreshKey={version} />}
    </>
  )
}
