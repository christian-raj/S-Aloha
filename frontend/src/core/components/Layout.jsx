import React, { useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { getUser } from '../../api'
import { MODULES, pillarOf } from '../../modules/registry'
import ThemeToggle from './ThemeToggle'
import { SOURCE_URL, LICENSE_LABEL } from '../about'
import { BrandMark, IconConsole, IconReport, IconLogout, IconMenu, IconClose, MODULE_ICONS } from './icons'

const ROLE_LABEL = { Admin: 'Administrateur', Manager: 'Gestionnaire', User: 'Utilisateur' }

const PILOTAGE = [
  { href: '/', label: 'Ma console', description: 'Ce qui requiert votre intervention', icon: IconConsole, end: true },
  { href: '/reports', label: 'Reporting', description: 'Indicateurs des processus', icon: IconReport },
]

function SectionLabel({ children, divider }) {
  return <p className={'nav-section' + (divider ? ' divider' : '')}><span className="dot" />{children}</p>
}

function NavItem({ href, label, description, icon: Icon, end, active, onClick }) {
  return (
    <NavLink to={href} end={end} onClick={onClick}
      className={({ isActive }) => 'nav-link' + ((active ?? isActive) ? ' active' : '')}>
      <span className="nav-icon"><Icon /></span>
      <span className="nav-text">
        <span className="nav-label">{label}</span>
        {description && <span className="nav-desc">{description}</span>}
      </span>
    </NavLink>
  )
}

function ModuleItem({ module, pathname, onClick }) {
  const Icon = MODULE_ICONS[module.id]
  const pillar = pillarOf(module.pillar)
  const tag = <span className="pillar-tag" title={`Pilier ${pillar.label}`}>{pillar.letter}</span>

  // Un processus sans interface reste visible : la feuille de route se lit
  // dans la navigation elle-même, sans laisser croire qu'il est utilisable.
  if (module.status !== 'active') {
    return (
      <div className="nav-link soon" aria-disabled="true" title="Processus en préparation">
        <span className="nav-icon"><Icon /></span>
        <span className="nav-text"><span className="nav-label">{module.label}</span></span>
        <span className="soon-badge">Bientôt</span>
      </div>
    )
  }

  const open = module.routes.some((r) => pathname === r || pathname.startsWith(r + '/'))
  return (
    <>
      <NavItem href={module.href} label={<>{module.label} {tag}</>} description={module.description}
        icon={Icon} active={open} onClick={onClick} />
      {open && module.pages.map((p) => (
        <NavLink key={p.href} to={p.href} onClick={onClick}
          className={({ isActive }) => 'nav-sublink' + (isActive || pathname.startsWith(p.href + '/') ? ' active' : '')}>
          {p.label}
        </NavLink>
      ))}
    </>
  )
}

function SidebarContent({ user, pathname, onClose, onLogout }) {
  return (
    <div className="sidebar-inner">
      <div className="sidebar-glow" aria-hidden />
      <div className="brand">
        <div className="brand-mark"><BrandMark /></div>
        <div>
          <p className="brand-name">S-Aloha</p>
          <p className="brand-sub">Excellence du service IT</p>
        </div>
      </div>

      <nav className="nav">
        <SectionLabel>Pilotage</SectionLabel>
        {PILOTAGE.map((item) => <NavItem key={item.href} {...item} onClick={onClose} />)}

        <SectionLabel divider>Processus ITIL</SectionLabel>
        {MODULES.map((m) => <ModuleItem key={m.id} module={m} pathname={pathname} onClick={onClose} />)}
      </nav>

      <div className="sidebar-foot">
        <div className="me">
          <div className="avatar">{(user?.displayName || '?').charAt(0).toUpperCase()}</div>
          <div className="me-text">
            <p className="me-name">{user?.displayName}</p>
            <p className="me-role">{ROLE_LABEL[user?.role] ?? user?.role}</p>
          </div>
          <ThemeToggle />
        </div>
        <button className="logout" onClick={onLogout}><IconLogout /> Se déconnecter</button>
        <a className="source-link" href={SOURCE_URL} target="_blank" rel="noreferrer">Code source · {LICENSE_LABEL}</a>
      </div>
    </div>
  )
}

export default function Layout() {
  const user = getUser()
  const nav = useNavigate()
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const logout = () => { sessionStorage.clear(); nav('/login') }

  return (
    <div className="shell">
      <aside className="sidebar">
        <SidebarContent user={user} pathname={pathname} onLogout={logout} />
      </aside>

      {open && (
        <div className="drawer" role="dialog" aria-modal="true" aria-label="Navigation">
          <div className="drawer-backdrop" onClick={() => setOpen(false)} />
          <div className="drawer-panel">
            <button className="drawer-close" onClick={() => setOpen(false)} aria-label="Fermer la navigation"><IconClose /></button>
            <SidebarContent user={user} pathname={pathname} onClose={() => setOpen(false)} onLogout={logout} />
          </div>
        </div>
      )}

      <div className="content">
        <header className="topbar">
          <button className="topbar-btn" onClick={() => setOpen(true)} aria-label="Ouvrir la navigation"><IconMenu /></button>
          <div className="brand-mark small"><BrandMark size={16} /></div>
          <span className="brand-name">S-Aloha</span>
          <button className="topbar-btn push" onClick={logout} aria-label="Se déconnecter" title="Se déconnecter"><IconLogout /></button>
        </header>
        <main className="main bg-canvas">
          <div className="main-inner"><Outlet /></div>
        </main>
      </div>
    </div>
  )
}
