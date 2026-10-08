import React, { useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { getUser } from '../../api'
import { MODULES, SECTIONS, visibleFor } from '../../modules/registry'
import ThemeToggle from './ThemeToggle'
import { SOURCE_URL, LICENSE_LABEL } from '../about'
import { BrandMark, IconConsole, IconReport, IconLogout, IconMenu, IconClose, MODULE_ICONS, SECTION_ICONS } from './icons'

const ROLE_LABEL = { Admin: 'Administrateur', Manager: 'Gestionnaire', User: 'Utilisateur' }

const PILOTAGE = [
  { href: '/', label: 'Ma console', description: 'Ce qui requiert votre intervention', icon: IconConsole, end: true },
  { href: '/reports', label: 'Reporting', description: 'Indicateurs des processus', icon: IconReport },
]

function SectionLabel({ children, divider, tone }) {
  return <p className={'nav-section' + (divider ? ' divider' : '') + (tone ? ' ' + tone : '')}><span className="dot" />{children}</p>
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

/** Chemin d'une sous-entrée, sans son filtre (`/problems?status=…` → `/problems`). */
const pathOf = (href) => href.split('?')[0]

function ModuleItem({ module, location, role, onClick }) {
  const Icon = MODULE_ICONS[module.id]
  const { pathname, search } = location

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
  const pages = (module.pages ?? []).filter((p) => visibleFor(p, role))
  // Une seule sous-entrée active : l'URL complète d'abord (« Erreurs connues »
  // est le registre filtré), puis le chemin seul (registre avec un autre
  // filtre), puis le préfixe (fiche /problems/12, sous-page /changes/schedule).
  const active = pages.find((p) => p.href === pathname + search)
    ?? pages.find((p) => p.href === pathname)
    ?? pages.find((p) => pathname.startsWith(pathOf(p.href) + '/'))
  return (
    <>
      <NavItem href={module.href} label={module.label} description={module.description}
        icon={Icon} active={open} onClick={onClick} />
      {open && pages.map((p) => (
        // Link, pas NavLink : NavLink marquerait « active » et aria-current
        // sur le seul chemin, donc le registre ET « Erreurs connues » (même
        // chemin, autre filtre). L'état actif est calculé ci-dessus.
        <Link key={p.href} to={p.href} onClick={onClick}
          aria-current={p === active ? 'page' : undefined}
          className={'nav-sublink' + (p === active ? ' active' : '')}>
          {p.label}
        </Link>
      ))}
    </>
  )
}

function SidebarContent({ user, location, onClose, onLogout }) {
  return (
    <div className="sidebar-inner">
      <div className="sidebar-glow" aria-hidden />
      <div className="brand">
        <div className="brand-mark"><BrandMark /></div>
        <div>
          <p className="brand-name">S-Aloha</p>
          <p className="brand-sub">Plateforme ITIL</p>
        </div>
      </div>

      <nav className="nav">
        <SectionLabel>Pilotage</SectionLabel>
        {PILOTAGE.filter((item) => visibleFor(item, user?.role))
          .map((item) => <NavItem key={item.href} {...item} onClick={onClose} />)}

        <SectionLabel divider>Pratiques</SectionLabel>
        {MODULES.filter((m) => visibleFor(m, user?.role)).map((m) =>
          <ModuleItem key={m.id} module={m} location={location} role={user?.role} onClick={onClose} />)}

        {SECTIONS.filter((s) => visibleFor(s, user?.role)).map((s) => {
          // Pas de section vide : sans entrée visible pour ce rôle, ni titre ni filet.
          const entries = s.entries.filter((e) => visibleFor(e, user?.role))
          if (entries.length === 0) return null
          return (
            <React.Fragment key={s.id}>
              <SectionLabel divider tone={s.id === 'administration' ? 'admin' : undefined}>{s.label}</SectionLabel>
              {entries.map((e) =>
                <NavItem key={e.href} href={e.href} label={e.label} description={e.description}
                  icon={SECTION_ICONS[e.icon] ?? IconConsole} onClick={onClose} />)}
            </React.Fragment>
          )
        })}
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
  const location = useLocation()
  const [open, setOpen] = useState(false)
  const logout = () => { sessionStorage.clear(); nav('/login') }

  return (
    <div className="shell">
      <aside className="sidebar">
        <SidebarContent user={user} location={location} onLogout={logout} />
      </aside>

      {open && (
        <div className="drawer" role="dialog" aria-modal="true" aria-label="Navigation">
          <div className="drawer-backdrop" onClick={() => setOpen(false)} />
          <div className="drawer-panel">
            <button className="drawer-close" onClick={() => setOpen(false)} aria-label="Fermer la navigation"><IconClose /></button>
            <SidebarContent user={user} location={location} onClose={() => setOpen(false)} onLogout={logout} />
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
