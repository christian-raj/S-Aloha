import React, { useEffect, useState } from 'react'
import { api } from '../../../api'
import RecordForm, { toForm, toPayload } from '../../../core/components/RecordForm'

/**
 * Registre d'éléments d'un projet (phases, jalons, livrables, risques,
 * incidents projet…) : tableau, ajout et modification dans un formulaire
 * décrit comme ceux des pratiques (RecordForm).
 * - `segment` : ressource de l'API (`/api/projects/{id}/{segment}`) ;
 * - `fields`, `columns` ; `canWrite` : droit d'ajouter et modifier ;
 *   `canDelete` : chef de projet ou gestionnaire ; `actions(row, reload)` :
 *   boutons propres (validation de fin d'étape…) ; `toForm(row)` : adapte une
 *   ligne au formulaire (date d'atteinte d'un jalon → case « Atteint »).
 */
export default function PartTable({ project, segment, title, hint, fields, columns, canWrite, canDelete, actions, empty, onChanged, toForm: adapt }) {
  const client = api.projects.parts(project.id, segment)
  const [rows, setRows] = useState(null)
  const [editing, setEditing] = useState(null)        // null, 'new' ou l'élément modifié
  const [form, setForm] = useState({})
  const [error, setError] = useState('')
  const frozen = ['Clôturé', 'Abandonné'].includes(project.status)
  const load = () => client.list().then(setRows).catch(e => setError(e.message))
  useEffect(() => { load() }, [project.id, segment])

  const open = (row) => { setEditing(row ?? 'new'); setForm(toForm(fields, row ? (adapt ? adapt(row) : row) : {})); setError('') }
  const save = () => (editing === 'new' ? client.create(toPayload(fields, form)) : client.update(editing.id, toPayload(fields, form)))
    .then(() => { setEditing(null); load(); onChanged?.() })
    .catch(e => setError(e.message))
  const remove = (row) => window.confirm('Supprimer cet élément ?')
    && client.remove(row.id).then(() => { load(); onChanged?.() }).catch(e => setError(e.message))

  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 6 }}>
        <h3 style={{ flex: 1 }}>{title}{rows && ` (${rows.length})`}</h3>
        {canWrite && !frozen && editing === null && <button className="btn small" onClick={() => open(null)}>+ Ajouter</button>}
      </div>
      {hint && <p className="muted small" style={{ marginBottom: 10 }}>{hint}</p>}
      {error && <div className="error">{error}</div>}
      {editing !== null && (
        <div className="part-form">
          <RecordForm fields={fields} form={form} onChange={patch => setForm(f => ({ ...f, ...patch }))} creating={editing === 'new'} />
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn small" onClick={save}>Enregistrer</button>
            <button className="btn ghost small" onClick={() => setEditing(null)}>Annuler</button>
          </div>
        </div>
      )}
      {rows === null ? <p className="muted small">Chargement…</p>
        : rows.length === 0 ? <p className="muted small">{empty}</p>
        : <table>
          <thead><tr>{columns.map(c => <th key={c.h}>{c.h}</th>)}<th /></tr></thead>
          <tbody>{rows.map(r => (
            <tr key={r.id}>
              {columns.map(c => <td key={c.h}>{c.v(r)}</td>)}
              <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                {actions?.(r, load, rows)}
                {canWrite && !frozen && <button className="btn ghost small" onClick={() => open(r)}>Modifier</button>}{' '}
                {canDelete && !frozen && <button className="btn ghost small" onClick={() => remove(r)} aria-label="Supprimer">×</button>}
              </td>
            </tr>))}
          </tbody>
        </table>}
    </div>
  )
}
