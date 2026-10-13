import React, { useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { getUser } from '../../api'
import { GROUPS, MODULES, SECTIONS, groupOf, visibleFor } from '../../modules/registry'
import ThemeToggle from './ThemeToggle'
import NotificationBell from './NotificationBell'
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

const COLLAPSED_KEY = 's-aloha-nav-collapsed'

/** Groupes repliés par l'utilisateur, mémorisés dans ce navigateur (confort, pas une donnée). */
function useCollapsed() {
  const [collapsed, setCollapsed] = useState(() => {
    try { return new Set(JSON.parse(localStorage.getItem(COLLAPSED_KEY) || '[]')) } catch { return new Set() }
  })
  const toggle = (id) => setCollapsed((prev) => {
    const next = new Set(prev)
    if (next.has(id)) next.delete(id); else next.add(id)
    try { localStorage.setItem(COLLAPSED_KEY, JSON.stringify([...next])) } catch { /* stockage indisponible : l'état reste en mémoire */ }
    return next
  })
  return [collapsed, toggle]
}

const Chevron = () => (
  <svg className="nav-chevron" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d="m6 9 6 6 6-6" /></svg>
)

/**
 * Section repliable de la barre latérale (Pratiques, Conformité &
 * réglementation, Paramétrage, Administration). La section qui contient la
 * page ouverte reste dépliée : on ne cache jamais où l'on se trouve.
 */
function NavGroup({ id, label, tone, current, collapsed, onToggle, children }) {
  const expanded = current || !collapsed.has(id)
  return (
    <div className="nav-group">
      <button type="button" className={'nav-section divider collapsible' + (tone ? ' ' + tone : '')}
        aria-expanded={expanded} aria-controls={'nav-group-' + id} aria-disabled={current || undefined}
        title={current ? 'Contient la page ouverte' : expanded ? 'Replier la section' : 'Déplier la section'}
        onClick={() => !current && onToggle(id)}>
        <span className="dot" /><span className="nav-section-label">{label}</span><Chevron />
      </button>
      {expanded && <div id={'nav-group-' + id} className="nav-group-items">{children}</div>}
    </div>
  )
}

/** La page ouverte relève-t-elle de ce préfixe d'URL ? */
const within = (pathname, prefix) => pathname === prefix || pathname.startsWith(prefix + '/')

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

function SidebarContent({ user, location, collapsed, onToggle, onClose, onLogout }) {
  const role = user?.role
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

        {GROUPS.map((g) => {
          // Pas de section vide : sans module visible pour ce rôle, ni titre ni filet.
          const modules = MODULES.filter((m) => groupOf(m) === g.id && visibleFor(m, role))
          if (modules.length === 0) return null
          const current = modules.some((m) => m.routes?.some((r) => within(location.pathname, r)))
          return (
            <NavGroup key={g.id} id={g.id} label={g.label} tone={g.tone} current={current}
              collapsed={collapsed} onToggle={onToggle}>
              {modules.map((m) => <ModuleItem key={m.id} module={m} location={location} role={role} onClick={onClose} />)}
            </NavGroup>
          )
        })}

        {SECTIONS.filter((s) => visibleFor(s, role)).map((s) => {
          const entries = s.entries.filter((e) => visibleFor(e, role))
          if (entries.length === 0) return null
          const current = entries.some((e) => within(location.pathname, e.href))
          return (
            <NavGroup key={s.id} id={s.id} label={s.label} tone={s.id === 'administration' ? 'admin' : undefined}
              current={current} collapsed={collapsed} onToggle={onToggle}>
              {entries.map((e) =>
                <NavItem key={e.href} href={e.href} label={e.label} description={e.description}
                  icon={SECTION_ICONS[e.icon] ?? IconConsole} onClick={onClose} />)}
            </NavGroup>
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
          <NotificationBell placement="side" onNavigate={onClose} />
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
  // Un seul état pour la barre latérale et le tiroir mobile.
  const [collapsed, toggleCollapsed] = useCollapsed()
  const logout = () => { sessionStorage.clear(); nav('/login') }

  return (
    <div className="shell">
      <aside className="sidebar">
        <SidebarContent user={user} location={location} collapsed={collapsed} onToggle={toggleCollapsed} onLogout={logout} />
      </aside>

      {open && (
        <div className="drawer" role="dialog" aria-modal="true" aria-label="Navigation">
          <div className="drawer-backdrop" onClick={() => setOpen(false)} />
          <div className="drawer-panel">
            <button className="drawer-close" onClick={() => setOpen(false)} aria-label="Fermer la navigation"><IconClose /></button>
            <SidebarContent user={user} location={location} collapsed={collapsed} onToggle={toggleCollapsed} onClose={() => setOpen(false)} onLogout={logout} />
          </div>
        </div>
      )}

      <div className="content">
        <header className="topbar">
          <button className="topbar-btn" onClick={() => setOpen(true)} aria-label="Ouvrir la navigation"><IconMenu /></button>
          <div className="brand-mark small"><BrandMark size={16} /></div>
          <span className="brand-name">S-Aloha</span>
          <span className="push"><NotificationBell placement="top" /></span>
          <button className="topbar-btn" onClick={logout} aria-label="Se déconnecter" title="Se déconnecter"><IconLogout /></button>
        </header>
        <main className="main bg-canvas">
          <div className="main-inner"><Outlet /></div>
        </main>
      </div>
    </div>
  )
}
