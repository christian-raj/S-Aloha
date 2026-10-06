import React, { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../../api'
import StatusBadge from '../../../core/components/StatusBadge'
import { dateTimeFr } from '../../../core/fields'
import { changeStatuses } from '../config'

/** Lundi de la semaine d'une date (clé de regroupement). */
const mondayOf = iso => {
  const d = new Date(iso)
  d.setHours(0, 0, 0, 0)
  d.setDate(d.getDate() - ((d.getDay() + 6) % 7))
  return d
}

/** Calendrier des changements : changements planifiés, regroupés par semaine. */
export default function ChangeSchedule() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState('')
  const nav = useNavigate()
  useEffect(() => { api.changes.schedule().then(setItems).catch(e => setError(e.message)) }, [])
  if (error) return <div className="error">{error}</div>
  if (!items) return <p>Chargement…</p>

  const weeks = []
  for (const c of items) {
    const key = mondayOf(c.plannedStart).getTime()
    const week = weeks.find(w => w.key === key)
    if (week) week.items.push(c); else weeks.push({ key, items: [c] })
  }

  return (
    <>
      <h1 className="page-title">Calendrier des changements</h1>
      <p className="page-sub">Changements planifiés (hors rejetés), de la semaine dernière à venir.</p>
      {weeks.length === 0 && <p style={{ color: 'var(--muted)' }}>Aucun changement planifié.</p>}
      {weeks.map(w => (
        <div className="card" key={w.key} style={{ marginBottom: 14 }}>
          <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>
            Semaine du {new Date(w.key).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })}
          </h3>
          <table>
            <thead><tr><th>Début</th><th>Fin</th><th>Référence</th><th>Titre</th><th>Type</th><th>Risque</th><th>Responsable</th><th>Statut</th></tr></thead>
            <tbody>{w.items.map(c => (
              <tr key={c.id} className="clickable" onClick={() => nav('/changes/' + c.id)}>
                <td>{dateTimeFr(c.plannedStart)}</td><td>{dateTimeFr(c.plannedEnd)}</td>
                <td><b>{c.reference}</b></td><td>{c.title}</td><td>{c.changeType}</td><td>{c.risk}</td>
                <td>{c.ownerDisplayName || '—'}</td>
                <td><StatusBadge statuses={changeStatuses} status={c.status} /></td>
              </tr>))}
            </tbody>
          </table>
        </div>
      ))}
    </>
  )
}
