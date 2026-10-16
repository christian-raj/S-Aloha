import React, { useEffect, useState } from 'react'
import { api, getUser } from '../../../api'

const EMPTY = { name: '', description: '', implementationPlan: '', backoutPlan: '', risk: 'Faible', active: true }

/**
 * CHG-26 : modèles de changement standard — plans types et risque, évalués une fois pour
 * toutes ; un changement Standard se crée depuis l'un d'eux et en est pré-autorisé.
 * Tenus par les gestionnaires ; désactivés plutôt que supprimés.
 */
export default function StandardChanges() {
  const [items, setItems] = useState(null)
  const [f, setF] = useState(null)
  const [error, setError] = useState('')
  const manager = ['Admin', 'Manager'].includes(getUser()?.role)
  const load = () => api.standardChanges.list(true).then(setItems).catch(e => setError(e.message))
  useEffect(() => { load() }, [])
  const set = k => e => setF({ ...f, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value })
  const save = () => (f.id ? api.standardChanges.update(f.id, f) : api.standardChanges.create(f))
    .then(() => { setF(null); setError(''); load() }).catch(e => setError(e.message))

  return (
    <>
      <h1 className="page-title">Modèles de changement standard</h1>
      <p className="page-sub">Changements courants, à faible risque, évalués une fois : un changement Standard se crée depuis un modèle et est pré-autorisé.</p>
      {error && <div className="error">{error}</div>}
      {manager && !f && <button className="btn" style={{ marginBottom: 16 }} onClick={() => setF(EMPTY)}>+ Nouveau modèle</button>}
      {f && (
        <div className="card" style={{ marginBottom: 16 }}>
          <div className="row2">
            <div className="field"><label htmlFor="std-name">Nom *</label>
              <input id="std-name" value={f.name} onChange={set('name')} placeholder="Redémarrage planifié d'un serveur…" /></div>
            <div className="field"><label htmlFor="std-risk">Risque</label>
              <select id="std-risk" value={f.risk} onChange={set('risk')}><option>Faible</option><option>Moyen</option><option>Élevé</option></select></div>
          </div>
          <div className="field"><label htmlFor="std-desc">Description</label>
            <textarea id="std-desc" rows={2} value={f.description} onChange={set('description')} /></div>
          <div className="row2">
            <div className="field"><label htmlFor="std-impl">Plan de mise en œuvre *</label>
              <textarea id="std-impl" rows={3} value={f.implementationPlan} onChange={set('implementationPlan')} /></div>
            <div className="field"><label htmlFor="std-back">Plan de retour arrière *</label>
              <textarea id="std-back" rows={3} value={f.backoutPlan} onChange={set('backoutPlan')} /></div>
          </div>
          <label style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 10 }}>
            <input type="checkbox" style={{ width: 'auto' }} checked={f.active} onChange={set('active')} /> Proposé (actif)</label>
          <button className="btn" onClick={save} disabled={!f.name.trim() || !f.implementationPlan.trim() || !f.backoutPlan.trim()}>Enregistrer</button>{' '}
          <button className="btn ghost" onClick={() => setF(null)}>Annuler</button>
        </div>)}
      {!items ? <p>Chargement…</p> : (
        <table>
          <thead><tr><th>Modèle</th><th>Risque</th><th>Statut</th>{manager && <th />}</tr></thead>
          <tbody>
            {items.map(m => (
              <tr key={m.id} style={m.active ? {} : { color: 'var(--muted)' }}>
                <td><b>{m.name}</b>{m.description && <div className="muted small">{m.description}</div>}</td>
                <td>{m.risk}</td><td>{m.active ? 'Proposé' : 'Désactivé'}</td>
                {manager && <td><button className="btn ghost small" onClick={() => setF({ ...EMPTY, ...m })}>Modifier</button></td>}
              </tr>))}
            {items.length === 0 && <tr><td colSpan={4} className="muted">Aucun modèle : sans modèle, pas de changement Standard.</td></tr>}
          </tbody>
        </table>)}
    </>
  )
}
