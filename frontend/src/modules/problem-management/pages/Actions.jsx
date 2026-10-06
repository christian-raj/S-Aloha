import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, getUser } from '../../../api'

export default function Actions() {
  const [items, setItems] = useState([])
  const [status, setStatus] = useState('')
  const [mine, setMine] = useState(false)
  const [error, setError] = useState('')
  const me = getUser()?.username

  const load = () => api.actions.all({ ...(status && { status }), ...(mine && { assignee: me }) })
    .then(setItems).catch(e => setError(e.message))
  useEffect(() => { load() }, [status, mine])

  const overdue = a => a.dueDate && !['Terminée', 'Annulée'].includes(a.status) && new Date(a.dueDate) < new Date()

  return (
    <>
      <h1 className="page-title">Actions correctives</h1>
      <p className="page-sub">Suivi transverse des actions et de leurs affectations RACI.</p>
      {error && <div className="error">{error}</div>}

      <div style={{ display: 'flex', gap: 10, marginBottom: 16, alignItems: 'center' }}>
        <select style={{ maxWidth: 180 }} value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Tous les statuts</option>
          <option>À faire</option><option>En cours</option><option>Terminée</option><option>Annulée</option>
        </select>
        <label style={{ display: 'flex', gap: 6, alignItems: 'center', margin: 0, fontWeight: 600 }}>
          <input type="checkbox" style={{ width: 'auto' }} checked={mine} onChange={e => setMine(e.target.checked)} />
          Mes affectations
        </label>
      </div>

      <table>
        <thead><tr><th>Action</th><th>Problème</th><th>Statut</th><th>Échéance</th><th>RACI</th></tr></thead>
        <tbody>
          {items.map(a => (
            <tr key={a.id}>
              <td><b>{a.title}</b></td>
              <td><Link to={'/problems/' + a.problem.id}>{a.problem.reference}</Link> — {a.problem.title}</td>
              <td>{a.status}</td>
              <td style={overdue(a) ? { color: 'var(--danger)', fontWeight: 700 } : {}}>
                {a.dueDate ? new Date(a.dueDate).toLocaleDateString('fr-FR') : '—'}
                {overdue(a) && ' ⚠'}
              </td>
              <td>{a.raci.map(r => (
                <span className="chip" key={r.id}>
                  <span className={'badge ' + r.role}>{r.role}</span>{r.assigneeDisplayName}
                </span>))}
              </td>
            </tr>
          ))}
          {items.length === 0 && <tr><td colSpan={5} style={{ color: 'var(--muted)' }}>Aucune action à afficher.</td></tr>}
        </tbody>
      </table>
    </>
  )
}
