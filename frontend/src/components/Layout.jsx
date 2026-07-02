import React from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { getUser } from '../api'

export default function Layout() {
  const user = getUser()
  const nav = useNavigate()
  const logout = () => { sessionStorage.clear(); nav('/login') }
  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">Gestion des <span>problèmes</span></div>
        <NavLink to="/" end>Ma console</NavLink>
        <NavLink to="/problems">Problèmes</NavLink>
        <NavLink to="/actions">Actions correctives</NavLink>
        <NavLink to="/reports">Reporting</NavLink>
        <div className="me">
          <b>{user?.displayName}</b>
          {user?.role === 'Admin' ? 'Administrateur' : user?.role === 'Manager' ? 'Gestionnaire' : 'Utilisateur'}
          <button onClick={logout}>Se déconnecter</button>
        </div>
      </aside>
      <main className="main"><Outlet /></main>
    </div>
  )
}
