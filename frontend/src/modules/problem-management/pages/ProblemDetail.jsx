import React, { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { api, getUser } from '../../../api'
import FiveWhys from '../components/FiveWhys'
import Ishikawa from '../components/Ishikawa'
import FtaTree from '../components/FtaTree'
import RaciEditor from '../../../core/components/RaciEditor'
import LinkedItems from '../../../core/components/LinkedItems'
import SimilarCases from '../../../core/components/SimilarCases'
import useTransitions from '../../../core/useTransitions'
import StatusField, { forceOptions } from '../../../core/components/StatusField'
import History from '../../../core/components/History'

const STATUSES = ['Nouveau', 'En analyse', 'Erreur connue', 'Résolu', 'Clos']
/** Codes de clôture (PRB-13) selon le statut d'origine ; une transition forcée les admet tous. */
const CLOSURE_CODES = ['Corrigé', 'Erreur connue acceptée', 'Doublon', 'Non retenu']
const CLOSURE_FROM = { 'Résolu': ['Corrigé'], 'Erreur connue': ['Erreur connue acceptée'], Nouveau: ['Doublon', 'Non retenu'] }
/** Libellés des champs au journal d'audit (SOC-20). */
const LABELS = {
  title: 'Titre', description: 'Description', impact: 'Impact', urgency: 'Urgence', priority: 'Priorité',
  category: 'Catégorie', affectedService: 'Service affecté', knownErrorWorkaround: 'Contournement',
  rootCause: 'Cause racine', closureCode: 'Code de clôture'
}
const METHODS = { FIVE_WHYS: '5 Pourquoi', ISHIKAWA: 'Ishikawa (6M)', FTA: 'Arbre des défaillances (FTA)' }
const badge = s => s.replace(/[ é]/g, m => (m === ' ' ? '' : 'e'))

export default function ProblemDetail() {
  const { id } = useParams()
  const [p, setP] = useState(null)
  const [tab, setTab] = useState('infos')
  const [error, setError] = useState('')
  const isManager = ['Admin', 'Manager'].includes(getUser()?.role)

  const load = () => api.problems.get(id).then(setP).catch(e => setError(e.message))
  // Une autre fiche (lien suivi depuis l'onglet Liens) s'ouvre sur ses Informations.
  useEffect(() => { setTab('infos'); load() }, [id])
  if (error) return <div className="error">{error}</div>
  if (!p) return <p>Chargement…</p>

  return (
    <>
      <p style={{ marginBottom: 8 }}><Link to="/problems">← Problèmes</Link></p>
      <h1 className="page-title">{p.reference} — {p.title}</h1>
      <p className="page-sub">
        <span className={'badge ' + badge(p.status)}>{p.status}</span>{' '}
        <span className={'badge ' + p.priority}>{p.priority}</span>{' '}
        Déclaré par {p.createdByDisplayName} le {new Date(p.createdAt).toLocaleDateString('fr-FR')}
        {p.resolvedAt && <> · Résolu le {new Date(p.resolvedAt).toLocaleDateString('fr-FR')}</>}
        {p.closureCode && <> · Clôture : {p.closureCode}</>}
      </p>

      {/* Onglets communs des fiches (docs/reference/frontend.md § Navigation
          cible) : Informations → propres à la pratique → Liens. */}
      <div className="tabs" role="tablist">
        {[['infos', 'Informations'], ['rca', 'Analyse de cause racine'],
          ['actions', `Actions correctives (${p.actions.length})`], ['links', 'Liens'], ['history', 'Historique']]
          .map(([k, l]) => <button key={k} role="tab" aria-selected={tab === k}
            className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{l}</button>)}
      </div>

      {tab === 'infos' && <><Infos p={p} isManager={isManager} onSaved={load} /><SimilarCases type="problem" id={p.id} /></>}
      {tab === 'rca' && <Rca p={p} onSaved={load} />}
      {tab === 'actions' && <ActionsTab p={p} onSaved={load} />}
      {tab === 'history' && <History type="problem" id={p.id} labels={LABELS} refreshKey={p.updatedAt} />}
      {tab === 'links' && <LinkedItems type="problem" id={p.id}
        hint="Incidents à l'origine, changement qui corrige, article d'erreur connue…" />}
    </>
  )
}

function Infos({ p, isManager, onSaved }) {
  const graph = useTransitions(api.problems.transitions)
  const [reason, setReason] = useState('')
  const [f, setF] = useState({
    title: p.title, description: p.description, status: p.status, impact: p.impact,
    urgency: p.urgency, category: p.category, affectedService: p.affectedService,
    knownErrorWorkaround: p.knownErrorWorkaround || '', rootCause: p.rootCause || '',
    closureCode: p.closureCode || ''
  })
  const [msg, setMsg] = useState('')
  const set = k => e => setF({ ...f, [k]: e.target.value })
  const force = forceOptions(graph, p.status, f.status, reason)
  const save = () => api.problems.update(p.id, f, force)
    .then(() => { setMsg('Modifications enregistrées.'); onSaved() })
    .catch(e => setMsg(e.message))

  return (
    <div className="card">
      {msg && <div className="error" style={{ background: 'var(--blue-soft)', color: 'var(--navy)' }}>{msg}</div>}
      <div className="field"><label>Titre</label>
        <input value={f.title} onChange={set('title')} disabled={!isManager} /></div>
      <div className="field"><label>Description</label>
        <textarea rows={4} value={f.description} onChange={set('description')} disabled={!isManager} /></div>
      <div className="row3">
        <div><StatusField statuses={STATUSES.map(value => ({ value }))} current={p.status} value={f.status}
          onChange={status => setF({ ...f, status })} graph={graph} isManager={isManager}
          isAdmin={getUser()?.role === 'Admin'} disabled={!isManager} reason={reason} onReason={setReason} /></div>
        <div className="field"><label>Impact</label>
          <select value={f.impact} onChange={set('impact')} disabled={!isManager}>
            <option>Faible</option><option>Moyen</option><option>Élevé</option></select></div>
        <div className="field"><label>Urgence</label>
          <select value={f.urgency} onChange={set('urgency')} disabled={!isManager}>
            <option>Faible</option><option>Moyenne</option><option>Élevée</option></select></div>
      </div>
      <div className="row2">
        <div className="field"><label>Catégorie</label>
          <input value={f.category} onChange={set('category')} disabled={!isManager} /></div>
        <div className="field"><label>Service affecté</label>
          <input value={f.affectedService} onChange={set('affectedService')} disabled={!isManager} /></div>
      </div>
      <div className="field"><label>Contournement (erreur connue)</label>
        <textarea rows={2} value={f.knownErrorWorkaround} onChange={set('knownErrorWorkaround')} disabled={!isManager}
          placeholder="Solution de contournement documentée si le problème devient une erreur connue." /></div>
      <div className="field"><label>Cause racine validée</label>
        <textarea rows={2} value={f.rootCause} onChange={set('rootCause')} disabled={!isManager}
          placeholder="Renseignée à l'issue de l'analyse. Obligatoire pour résoudre." /></div>
      {f.status === 'Clos' && (
        <div className="field" style={{ maxWidth: 300 }}><label>Code de clôture *</label>
          <select value={f.closureCode} onChange={set('closureCode')} disabled={!isManager}>
            <option value="">—</option>
            {(p.status === 'Clos' ? CLOSURE_CODES : CLOSURE_FROM[p.status] ?? CLOSURE_CODES)
              .map(c => <option key={c}>{c}</option>)}
          </select></div>
      )}
      {isManager
        ? <button className="btn" onClick={save} disabled={force && !reason.trim()}>Enregistrer</button>
        : <p style={{ color: 'var(--muted)', fontSize: 13 }}>Seuls les gestionnaires de problèmes peuvent modifier ces informations.</p>}
    </div>
  )
}

function Rca({ p, onSaved }) {
  const [current, setCurrent] = useState(p.analyses[p.analyses.length - 1] || null)
  // Initialisation paresseuse : passée en valeur, l'expression était
  // réévaluée à CHAQUE rendu, et une analyse qu'on démarre (sans dataJson)
  // faisait planter l'onglet — aucune analyse ne pouvait être créée (B1).
  const [data, setData] = useState(() => (current ? JSON.parse(current.dataJson) : {}))
  const [msg, setMsg] = useState('')

  const start = (method) => { setCurrent({ method, id: null }); setData({}) }
  const open = (a) => { setCurrent(a); setData(JSON.parse(a.dataJson)) }

  const save = async () => {
    const dto = { method: current.method, dataJson: JSON.stringify(data), conclusion: data.rootCause || null }
    try {
      const saved = current.id
        ? await api.analyses.update(p.id, current.id, dto)
        : await api.analyses.create(p.id, dto)
      setCurrent(saved); setMsg('Analyse enregistrée.'); onSaved()
    } catch (e) { setMsg(e.message) }
  }

  return (
    <div className="card">
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 16 }}>
        {p.analyses.map(a => (
          <button key={a.id} className={'btn small ' + (current?.id === a.id ? '' : 'ghost')} onClick={() => open(a)}>
            {METHODS[a.method]} — {new Date(a.createdAt).toLocaleDateString('fr-FR')}
          </button>
        ))}
        <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          {Object.entries(METHODS).map(([k, l]) =>
            <button key={k} className="btn ghost small" onClick={() => start(k)}>+ {l}</button>)}
        </span>
      </div>

      {!current && <p style={{ color: 'var(--muted)' }}>
        Choisissez une méthodologie de recherche de cause racine pour démarrer une analyse.</p>}

      {current && (
        <>
          <h3 style={{ color: 'var(--navy)', marginBottom: 14 }}>{METHODS[current.method]}</h3>
          {msg && <div className="error" style={{ background: 'var(--blue-soft)', color: 'var(--navy)' }}>{msg}</div>}
          {current.method === 'FIVE_WHYS' && <FiveWhys data={data} onChange={setData} />}
          {current.method === 'ISHIKAWA' && <Ishikawa data={data} onChange={setData} />}
          {current.method === 'FTA' && <FtaTree data={data} onChange={setData} />}
          <button className="btn" style={{ marginTop: 16 }} onClick={save}>Enregistrer l'analyse</button>
        </>
      )}
    </div>
  )
}

function ActionsTab({ p, onSaved }) {
  const [showForm, setShowForm] = useState(false)
  const [f, setF] = useState({ title: '', description: '', dueDate: '', raci: [] })
  const [msg, setMsg] = useState('')

  const submit = () => api.actions.create(p.id, {
    ...f, dueDate: f.dueDate ? new Date(f.dueDate).toISOString() : null
  }).then(() => { setShowForm(false); setF({ title: '', description: '', dueDate: '', raci: [] }); onSaved() })
    .catch(e => setMsg(e.message))

  const setStatus = (a, status) =>
    api.actions.update(a.id, { title: a.title, description: a.description, status, dueDate: a.dueDate, raci: a.raci })
      .then(onSaved).catch(e => setMsg(e.message))

  return (
    <>
      {msg && <div className="error">{msg}</div>}
      <button className="btn" style={{ marginBottom: 16 }} onClick={() => setShowForm(v => !v)}>
        {showForm ? 'Fermer' : '+ Nouvelle action corrective'}
      </button>

      {showForm && (
        <div className="card" style={{ marginBottom: 20 }}>
          <div className="field"><label>Titre</label>
            <input value={f.title} onChange={e => setF({ ...f, title: e.target.value })} /></div>
          <div className="field"><label>Description</label>
            <textarea rows={3} value={f.description} onChange={e => setF({ ...f, description: e.target.value })} /></div>
          <div className="field" style={{ maxWidth: 220 }}><label>Échéance</label>
            <input type="date" value={f.dueDate} onChange={e => setF({ ...f, dueDate: e.target.value })} /></div>
          <label>Matrice RACI</label>
          <RaciEditor value={f.raci} onChange={raci => setF({ ...f, raci })} />
          <button className="btn" style={{ marginTop: 14 }} onClick={submit} disabled={!f.title}>Créer l'action</button>
        </div>
      )}

      {p.actions.map(a => (
        <div className="card" key={a.id} style={{ marginBottom: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <b style={{ flex: 1 }}>{a.title}</b>
            <select style={{ maxWidth: 150 }} value={a.status} onChange={e => setStatus(a, e.target.value)}>
              <option>À faire</option><option>En cours</option><option>Terminée</option><option>Annulée</option>
            </select>
          </div>
          <p style={{ fontSize: 13, color: 'var(--muted)', margin: '6px 0' }}>
            {a.description} {a.dueDate && <> — Échéance : {new Date(a.dueDate).toLocaleDateString('fr-FR')}</>}
          </p>
          <div>
            {a.raci.map(r => (
              <span className="chip" key={r.id}>
                <span className={'badge ' + r.role}>{r.role}</span>
                {r.assigneeType === 'Group' ? '👥' : '👤'} {r.assigneeDisplayName}
              </span>
            ))}
          </div>
        </div>
      ))}
      {p.actions.length === 0 && !showForm &&
        <p style={{ color: 'var(--muted)' }}>Aucune action corrective pour l'instant.</p>}
    </>
  )
}
