import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import { dateFr } from '../../fields'

const ROLE_TONE = { Admin: 't-bad', Manager: 't-progress', User: 't-closed' }

/** Administration → Utilisateurs et rôles (ADM-10 à ADM-13) : membres des groupes AD de rôle. */
export default function Users() {
  const [data, setData] = useState(null)
  const [q, setQ] = useState('')
  const [role, setRole] = useState('')
  const [error, setError] = useState('')
  const load = () => api.admin.users({ ...(q && { q }), ...(role && { role }) }).then(d => { setData(d); setError('') }).catch(e => setError(e.message))
  useEffect(() => { load() }, [role])

  return (
    <>
      <h1 className="page-title">Utilisateurs et rôles</h1>
      <p className="page-sub">Les membres des groupes AD de rôle, lus dans l'annuaire : la plateforme ne stocke aucun compte, seulement la date de dernière connexion (13 mois).</p>
      {error && <div className="error" role="alert">{error}</div>}
      {data && (
        <div className="card" style={{ marginBottom: 16 }}>
          <h3 className="card-title">Groupes AD → rôles</h3>
          <ul className="plain-list">{data.mapping.map(m => (
            <li key={m.role}><span className={'badge ' + ROLE_TONE[m.role]}>{m.role}</span> <span className="mono">{m.group ?? '—'}</span></li>))}
          </ul>
          <p className="muted small" style={{ marginTop: 8 }}>Réglé dans la configuration de l'API (<code>Ldap:Groups</code>) ; les affectations aux périmètres se font dans <Link to="/admin/perimeters">Périmètres</Link>.</p>
        </div>
      )}
      {data?.withoutAccess.length > 0 && (
        <div className="notice warn" role="status">
          <b>{data.withoutAccess.length} affectation(s) « sans accès »</b> : ces comptes ne sont dans aucun groupe de rôle et ne peuvent pas se connecter.
          <ul className="plain-list" style={{ marginTop: 6 }}>{data.withoutAccess.map(o => (
            <li key={o.id}>{o.principalDisplayName} <span className="muted mono">{o.principalId}</span> — {o.role} sur {o.perimeter}</li>))}</ul>
        </div>
      )}
      <div className="toolbar">
        <input style={{ maxWidth: 260 }} placeholder="Nom ou compte…" value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && load()} aria-label="Rechercher un utilisateur" />
        <select style={{ maxWidth: 160 }} value={role} onChange={e => setRole(e.target.value)} aria-label="Rôle">
          <option value="">Tous les rôles</option>{['Admin', 'Manager', 'User'].map(r => <option key={r}>{r}</option>)}
        </select>
        {data && <span className="muted small">{data.users.length} utilisateur(s)</span>}
      </div>
      {!data && !error && <p>Lecture de l'annuaire…</p>}
      {data && (
        <div className="table-scroll">
          <table>
            <thead><tr><th>Utilisateur</th><th>Rôle AD</th><th>Rôle par périmètre</th><th>Dernière connexion</th></tr></thead>
            <tbody>
              {data.users.map(u => (
                <tr key={u.username}>
                  <td><b>{u.displayName}</b><div className="muted small mono">{u.username}</div></td>
                  <td><span className={'badge ' + ROLE_TONE[u.role]}>{u.role}</span></td>
                  <td className="small">{u.perimeters === null ? 'Tous (portée globale)'
                    : u.perimeters.map(p => <div key={p.id}><b>{p.name}</b> : {p.role}{p.via.length > 0 && <span className="muted"> ({p.via.join(', ')})</span>}</div>)}</td>
                  <td className="small">{u.lastLoginAt ? dateFr(u.lastLoginAt) : <span className="muted">Jamais connecté (ou plus de 13 mois)</span>}</td>
                </tr>))}
              {data.users.length === 0 && <tr><td colSpan={4} className="muted">Aucun utilisateur ne correspond.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
