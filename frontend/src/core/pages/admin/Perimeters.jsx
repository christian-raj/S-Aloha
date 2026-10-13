import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import { dateFr } from '../../fields'

/**
 * Administration › Périmètres (PER-01, ADR-0014) : liste des périmètres — supprimés
 * compris, grisés et restaurables —, création. La fiche porte le reste.
 */
export default function Perimeters() {
  const [list, setList] = useState(null)
  const [error, setError] = useState('')
  const [f, setF] = useState({ name: '', description: '' })
  const load = () => api.perimeters.list().then(setList).catch(e => setError(e.message))
  useEffect(() => { load() }, [])
  const create = () => api.perimeters.create(f)
    .then(() => { setF({ name: '', description: '' }); setError(''); load() })
    .catch(e => setError(e.message))
  const restore = (id) => api.perimeters.restore(id).then(load).catch(e => setError(e.message))

  return (
    <>
      <h1 className="page-title">Périmètres</h1>
      <p className="page-sub">
        Directions, sites ou entités : chacun voit et décide dans ses périmètres. Un compte sans affectation garde son
        rôle AD sur « Organisation ».
      </p>
      {error && <div className="error">{error}</div>}

      <div className="card" style={{ marginBottom: 20 }}>
        <h3 style={{ color: 'var(--navy)', marginBottom: 12 }}>Nouveau périmètre</h3>
        <div className="row2">
          <div className="field"><label>Nom *</label>
            <input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} placeholder="Ex. : Usine de Toamasina" /></div>
          <div className="field"><label>Description</label>
            <input value={f.description} onChange={e => setF({ ...f, description: e.target.value })} placeholder="Ex. : site de production" /></div>
        </div>
        <button className="btn" onClick={create} disabled={!f.name.trim()}>Créer</button>
      </div>

      {!list ? <p>Chargement…</p> : (
        <table>
          <thead><tr><th>Périmètre</th><th>Description</th><th>Affectations</th><th>État</th><th /></tr></thead>
          <tbody>
            {list.map(p => (
              <tr key={p.id} style={p.deletedAt ? { opacity: 0.55 } : undefined}>
                <td><Link to={'/admin/perimeters/' + p.id}><b>{p.name}</b></Link></td>
                <td>{p.description || '—'}</td>
                <td>{p.assignments}</td>
                <td>{p.deletedAt ? `Supprimé le ${dateFr(p.deletedAt)}` : 'En service'}</td>
                <td style={{ textAlign: 'right' }}>
                  {p.deletedAt && <button className="btn ghost small" onClick={() => restore(p.id)}>Restaurer</button>}
                </td>
              </tr>))}
          </tbody>
        </table>
      )}
    </>
  )
}
