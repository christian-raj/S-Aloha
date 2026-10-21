import React, { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '../../../api'
import PerimeterSelect from '../../../core/components/PerimeterSelect'
import usePerimeters from '../../../core/usePerimeters'
import useServices from '../../../core/useServices'
import ServiceSelect from '../../../core/components/ServiceSelect'
import CategoryInput from '../../../core/components/CategoryInput'
import useCategories from '../../../core/useCategories'

const STATUSES = ['Nouveau', 'En analyse', 'Erreur connue', 'Résolu', 'Clos']
const badge = s => s.replace(/[ é]/g, m => (m === ' ' ? '' : 'e'))

export default function Problems() {
  const [items, setItems] = useState([])
  // Le statut vit dans l'URL : « Erreurs connues » (navigation) est ce
  // registre filtré, et un filtre choisi ici se partage par simple lien.
  const [params, setParams] = useSearchParams()
  const status = params.get('status') ?? ''
  const setStatus = (s) => setParams(s ? { status: s } : {}, { replace: true })
  const [q, setQ] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [error, setError] = useState('')
  const nav = useNavigate()

  const load = () => api.problems.list({ ...(status && { status }), ...(q && { q }) })
    .then(setItems).catch(e => setError(e.message))
  useEffect(() => { load() }, [status])

  return (
    <>
      <h1 className="page-title">{status === 'Erreur connue' ? 'Erreurs connues' : 'Problèmes'}</h1>
      <p className="page-sub">{status === 'Erreur connue'
        ? 'Problèmes dont la cause est identifiée et le contournement documenté, en attente de correction définitive.'
        : 'Enregistrement et suivi des problèmes (ITIL).'}</p>
      {error && <div className="error">{error}</div>}

      <div style={{ display: 'flex', gap: 10, marginBottom: 16 }}>
        <input style={{ maxWidth: 260 }} placeholder="Rechercher (titre, référence)…"
          value={q} onChange={e => setQ(e.target.value)} onKeyDown={e => e.key === 'Enter' && load()} />
        <select style={{ maxWidth: 180 }} value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Tous les statuts</option>
          {STATUSES.map(s => <option key={s}>{s}</option>)}
        </select>
        <button className="btn" style={{ marginLeft: 'auto' }} onClick={() => setShowForm(v => !v)}>
          {showForm ? 'Fermer' : '+ Déclarer un problème'}
        </button>
      </div>

      {showForm && <NewProblem onCreated={p => { setShowForm(false); nav('/problems/' + p.id) }} />}

      <table>
        <thead><tr>
          <th>Référence</th><th>Titre</th><th>Statut</th><th>Priorité</th>
          <th>Catégorie</th><th>Déclaré par</th><th>Actions</th>
        </tr></thead>
        <tbody>
          {items.map(p => (
            <tr key={p.id} className="clickable" onClick={() => nav('/problems/' + p.id)}>
              <td><b>{p.reference}</b></td>
              <td>{p.title}</td>
              <td><span className={'badge ' + badge(p.status)}>{p.status}</span></td>
              <td><span className={'badge ' + p.priority}>{p.priority}</span></td>
              <td>{p.category}</td>
              <td>{p.createdByDisplayName}</td>
              <td>{p.actionsCount}</td>
            </tr>
          ))}
          {items.length === 0 && <tr><td colSpan={7} style={{ color: 'var(--muted)' }}>
            Aucun problème enregistré. Déclarez le premier avec le bouton ci-dessus.</td></tr>}
        </tbody>
      </table>
    </>
  )
}

function NewProblem({ onCreated }) {
  const [f, setF] = useState({ title: '', description: '', impact: 'Moyen', urgency: 'Moyenne', category: '', affectedService: '', serviceId: '', source: 'Réactif' })
  const services = useServices() ?? []
  const categories = useCategories('problem')
  const [error, setError] = useState('')
  const [perimeterId, setPerimeterId] = useState(null)
  const perimeters = usePerimeters()
  const set = k => e => setF({ ...f, [k]: e.target.value })
  const submit = () => api.problems.create({ ...f, serviceId: f.serviceId ? Number(f.serviceId) : null, perimeterId }).then(onCreated).catch(e => setError(e.message))
  return (
    <div className="card" style={{ marginBottom: 20 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 14 }}>Déclarer un problème</h3>
      {error && <div className="error">{error}</div>}
      <PerimeterSelect perimeters={perimeters} value={perimeterId} onChange={setPerimeterId} required />
      <div className="field"><label>Titre</label>
        <input value={f.title} onChange={set('title')} placeholder="Ex. : Latence récurrente sur l'ERP en fin de mois" /></div>
      <div className="field"><label>Description</label>
        <textarea rows={4} value={f.description} onChange={set('description')}
          placeholder="Symptômes observés, incidents liés, périmètre impacté…" /></div>
      {/* PRB-20 : réactif (issu d'incidents, à relier avant l'analyse) ou proactif. */}
      <div className="field"><label>Source</label>
        <select value={f.source} onChange={set('source')} aria-label="Source">
          <option value="Réactif">Réactif — issu d’incidents (à relier avant l’analyse)</option>
          <option value="Proactif">Proactif — tendance, analyse de risque</option>
        </select></div>
      <div className="row2">
        <div className="field"><label>Catégorie</label>
          <CategoryInput categories={categories} value={f.category} onChange={set('category')}
            placeholder="Réseau, Serveur, Application…" /></div>
        <div className="field"><label>Service affecté</label>
          <ServiceSelect services={services} value={f.serviceId} onChange={set('serviceId')} /></div>
      </div>
      <div className="row2">
        <div className="field"><label>Impact</label>
          <select value={f.impact} onChange={set('impact')}>
            <option>Faible</option><option>Moyen</option><option>Élevé</option></select></div>
        <div className="field"><label>Urgence</label>
          <select value={f.urgency} onChange={set('urgency')}>
            <option>Faible</option><option>Moyenne</option><option>Élevée</option></select></div>
      </div>
      <button className="btn" onClick={submit} disabled={!f.title || (perimeters.length > 1 && !perimeterId)}>Enregistrer le problème</button>
    </div>
  )
}
