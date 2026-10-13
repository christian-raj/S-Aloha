import React, { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '../../../api'
import DirectoryPicker from '../../components/DirectoryPicker'
import { dateFr } from '../../fields'

/**
 * Fiche d'un périmètre (PER-01, PER-02, PER-12) : nom et description, enregistrements
 * rattachés par pratique, affectations d'utilisateurs ou de groupes AD (Manager ou
 * User), suppression douce, restauration, suppression définitive si rien n'y est rattaché.
 */
export default function PerimeterDetail() {
  const { id } = useParams()
  const nav = useNavigate()
  const [p, setP] = useState(null)
  const [f, setF] = useState(null)
  const [role, setRole] = useState('User')
  const [msg, setMsg] = useState(null)
  const load = () => api.perimeters.get(id)
    .then(x => { setP(x); setF({ name: x.name, description: x.description }) })
    .catch(e => setMsg({ ok: false, text: e.message }))
  useEffect(() => { load() }, [id])
  if (!p) return msg ? <div className="error">{msg.text}</div> : <p>Chargement…</p>

  const act = (call, ok, after = load) => call.then(r => { setMsg(ok && { ok: true, text: ok }); after(r) })
    .catch(e => setMsg({ ok: false, text: e.message }))
  const assign = (entry) => act(api.perimeters.assign(p.id,
    { principalType: entry.type, principalId: entry.id, principalDisplayName: entry.displayName, role }), null)
  const total = p.counts.reduce((n, c) => n + c.count, 0)
  const purge = () => window.confirm(`Supprimer définitivement le périmètre « ${p.name} » ?`)
    && act(api.perimeters.purge(p.id), null, () => nav('/admin/perimeters'))

  return (
    <>
      <p style={{ marginBottom: 8 }}><Link to="/admin/perimeters">← Périmètres</Link></p>
      <h1 className="page-title">{p.name}</h1>
      <p className="page-sub">
        {p.deletedAt ? `Supprimé le ${dateFr(p.deletedAt)} par ${p.deletedBy}` : 'En service'}
        {' · '}Créé le {dateFr(p.createdAt)}{p.isDefault && ' · périmètre par défaut'}
      </p>
      {msg && <div className="error" style={msg.ok ? { background: 'var(--blue-soft)', color: 'var(--navy)' } : undefined}>{msg.text}</div>}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="row2">
          <div className="field"><label>Nom</label>
            <input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} /></div>
          <div className="field"><label>Description</label>
            <input value={f.description} onChange={e => setF({ ...f, description: e.target.value })} /></div>
        </div>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          <button className="btn" onClick={() => act(api.perimeters.update(p.id, f), 'Périmètre enregistré.')} disabled={!f.name.trim()}>Enregistrer</button>
          {!p.isDefault && (p.deletedAt
            ? <>
              <button className="btn ghost" onClick={() => act(api.perimeters.restore(p.id), 'Périmètre restauré.')}>Restaurer</button>
              <button className="btn danger" style={{ marginLeft: 'auto' }} onClick={purge}
                disabled={total > 0 || p.assignments.length > 0}
                title={total > 0 || p.assignments.length > 0 ? 'Des enregistrements ou des affectations y sont encore rattachés' : undefined}>
                Supprimer définitivement</button>
            </>
            : <button className="btn ghost" style={{ marginLeft: 'auto' }}
                onClick={() => act(api.perimeters.remove(p.id), 'Périmètre supprimé : il quitte les sélecteurs, ses enregistrements restent.')}>
                Supprimer</button>)}
        </div>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <h3 style={{ color: 'var(--navy)', marginBottom: 8 }}>Enregistrements rattachés ({total})</h3>
        <table>
          <tbody>{p.counts.map(c => <tr key={c.practice}><td>{c.practice}</td><td style={{ textAlign: 'right' }}>{c.count}</td></tr>)}</tbody>
        </table>
      </div>

      <div className="card">
        <h3 style={{ color: 'var(--navy)', marginBottom: 4 }}>Affectations ({p.assignments.length})</h3>
        <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 10 }}>
          Un utilisateur ou un groupe AD, Manager (décisions de gestionnaire) ou User. Le rôle par périmètre prime sur le
          rôle AD ; l'administrateur a une portée globale et ne s'affecte pas.</p>
        {p.assignments.length > 0 && (
          <table style={{ marginBottom: 12 }}>
            <thead><tr><th>Utilisateur ou groupe</th><th>Rôle</th><th /></tr></thead>
            <tbody>{p.assignments.map(a => (
              <tr key={a.id}>
                <td>{a.principalType === 'Group' ? '👥' : '👤'} {a.principalDisplayName} <span className="muted">({a.principalId})</span></td>
                <td><span className={'badge ' + (a.role === 'Manager' ? 't-progress' : 't-new')}>{a.role}</span></td>
                <td style={{ textAlign: 'right' }}>
                  <button className="btn ghost small" title="Retirer l'affectation"
                    onClick={() => act(api.perimeters.unassign(a.id), null)}>×</button>
                </td>
              </tr>))}
            </tbody>
          </table>
        )}
        {!p.deletedAt && (
          <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', flexWrap: 'wrap' }}>
            <select value={role} onChange={e => setRole(e.target.value)} style={{ width: 'auto' }} aria-label="Rôle de l'affectation">
              <option value="User">User</option><option value="Manager">Manager</option>
            </select>
            <div style={{ flex: 1, minWidth: 240 }}>
              <DirectoryPicker onPick={assign} placeholder="Affecter un utilisateur ou un groupe de l'annuaire…" />
            </div>
          </div>
        )}
      </div>
    </>
  )
}
