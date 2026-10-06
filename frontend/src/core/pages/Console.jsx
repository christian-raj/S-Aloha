import React, { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, getUser } from '../../api'
import { ITEM_TYPES, hrefOf } from '../itemTypes'

const badge = s => s.replace(/[ é]/g, m => (m === ' ' ? '' : 'e'))
const dateFr = d => new Date(d).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })
const today = new Date().toLocaleDateString('fr-FR', { weekday: 'long', day: 'numeric', month: 'long' })

const ROLE = {
  User: { label: 'Utilisateur', intro: 'Vos actions et les dossiers dont vous êtes responsable, du plus urgent au moins urgent.' },
  Manager: { label: 'Gestionnaire', intro: 'Ce qui est urgent, ce qui attend votre décision, puis ce qu’il faut relancer.' },
  Admin: { label: 'Administrateur', intro: 'Ce qui est urgent, ce qui attend votre décision, puis ce qu’il faut relancer.' },
}

const Chevron = () => (
  <svg className="row-chevron" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d="M9 6l6 6-6 6" /></svg>
)
const Check = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
    strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d="M5 12.5l4.5 4.5L19 7.5" /></svg>
)

/** Une liste du tableau de bord : titre, aide, lignes compactes cliquables.
 * Masquée si vide — la console ne montre que ce qui demande quelque chose. */
function List({ title, hint, tone, rows, render, onRow }) {
  if (rows.length === 0) return null
  return (
    <section className={'c-list tone-' + tone}>
      <header>
        <h3>{title} <span className="c-count">{rows.length}</span></h3>
        {hint && <p>{hint}</p>}
      </header>
      <ul>
        {rows.map(r => (
          <li key={(r.type ?? '') + r.id}>
            <button type="button" onClick={() => onRow(r)}>
              {render(r)}
              <Chevron />
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}

/** Contenu d'une ligne : référence, titre, puis métadonnées alignées à droite. */
const Row = ({ reference, title, meta }) => (
  <>
    <span className="row-ref">{reference}</span>
    <span className="row-title">{title}</span>
    <span className="row-meta">{meta}</span>
  </>
)

const Roles = ({ roles }) => roles.map(x => <span key={x} className={'badge ' + x}>{x}</span>)

/** Groupe de la console (Urgent, Décisions…) : n'apparaît que s'il a du contenu. */
function Group({ id, title, total, children }) {
  if (total === 0) return null
  return (
    <section className="c-group" id={id} aria-labelledby={id + '-t'}>
      <h2 id={id + '-t'}>{title}</h2>
      {children}
    </section>
  )
}

/** Console orientée action : ce qui requiert une intervention d'abord, le suivi ensuite. */
export default function Console() {
  const [c, setC] = useState(null)
  const [error, setError] = useState('')
  const nav = useNavigate()
  useEffect(() => { api.console.get().then(setC).catch(e => setError(e.message)) }, [])
  if (error) return <div className="error">{error}</div>
  if (!c) return <p className="muted">Chargement…</p>

  const goP = r => nav('/problems/' + (r.problem ? r.problem.id : r.id))
  const goItem = r => nav(hrefOf(r.type, r.id))
  const m = c.management
  const role = ROLE[c.role] ?? ROLE.User
  const firstName = (getUser()?.displayName || '').split(' ')[0]

  const myOverdue = c.myActions.filter(a => a.overdue)
  const myTodo = c.myActions.filter(a => !a.overdue)
  const L = k => (m ? m[k].length : 0)

  // Les quatre temps de la console, dans l'ordre où l'on doit les traiter.
  const urgent = myOverdue.length + L('majorIncidents')
  const decide = L('decisions') + L('toQualify')
  const chase = L('overdueActions') + L('knownErrorsNoAction') + L('reviewsDue')
  const work = myTodo.length + c.myAssignments.length
  const follow = c.myProblems.length + L('inAnalysisNoRootCause')

  const tiles = [
    { id: 'urgent', label: 'Urgent', caption: 'Retards et incidents majeurs', n: urgent, tone: 'red' },
    m && { id: 'decisions', label: 'Décisions', caption: 'À autoriser, approuver, qualifier', n: decide, tone: 'blue' },
    m && { id: 'relances', label: 'Relances', caption: 'Retards d’équipe, revues échues', n: chase, tone: 'amber' },
    { id: 'travail', label: 'Mon travail', caption: 'Actions et dossiers en cours', n: work, tone: 'emerald' },
  ].filter(Boolean)

  const itemRow = r => <Row reference={r.reference}
    title={r.title}
    meta={<><span className="row-kind">{ITEM_TYPES[r.type]?.label ?? r.type}</span><span className="row-status">{r.status}</span></>} />

  return (
    <>
      <header className="c-head">
        <p className="c-eyebrow">{today} · Console {role.label.toLowerCase()}</p>
        <h1 className="page-title">{firstName ? `Bonjour, ${firstName}` : 'Ma console'}</h1>
        <p className="page-sub">{role.intro}</p>
      </header>

      <nav className="c-tiles" aria-label="Synthèse">
        {tiles.map(t => (
          <a key={t.id} href={'#' + t.id} className={'c-tile tone-' + t.tone + (t.n === 0 ? ' is-zero' : '')}
            onClick={e => { e.preventDefault(); document.getElementById(t.id)?.scrollIntoView({ behavior: 'smooth' }) }}
            aria-disabled={t.n === 0}>
            <span className="c-tile-n">{t.n}</span>
            <span className="c-tile-l">{t.label}</span>
            <span className="c-tile-c">{t.caption}</span>
          </a>
        ))}
      </nav>

      {urgent + decide + chase + work === 0 && (
        <div className="c-clear">
          <span className="c-clear-icon"><Check /></span>
          <div>
            <b>Tout est à jour</b>
            <p>Aucune intervention ne vous est demandée pour le moment.</p>
          </div>
        </div>
      )}

      <Group id="urgent" title="Urgent" total={urgent}>
        <List title="Mes actions en retard" tone="red" rows={myOverdue} onRow={goP}
          hint="Échéance dépassée : à traiter ou à replanifier."
          render={r => <Row reference={r.problem.reference} title={r.title}
            meta={<><Roles roles={r.myRoles} /><span className="row-date is-late">{dateFr(r.dueDate)}</span></>} />} />
        {m && <List title="Incidents majeurs en cours" tone="red" rows={m.majorIncidents} onRow={goItem}
          hint="Coordonner le rétablissement et la communication." render={itemRow} />}
      </Group>

      {m && <Group id="decisions" title="Décisions" total={decide}>
        <List title="En attente de votre décision" tone="blue" rows={m.decisions} onRow={goItem}
          hint="Changements à autoriser, demandes à approuver, améliorations à valider." render={itemRow} />
        <List title="Problèmes à qualifier" tone="blue" rows={m.toQualify} onRow={goP}
          hint="Nouveaux problèmes : impact, urgence et catégorie à confirmer."
          render={r => <Row reference={r.reference} title={r.title}
            meta={<><span className={'badge ' + r.priority}>{r.priority}</span><span className="row-who">{r.createdByDisplayName}</span><span className="row-date">{dateFr(r.createdAt)}</span></>} />} />
      </Group>}

      {m && <Group id="relances" title="Relances" total={chase}>
        <List title="Actions en retard, toutes équipes" tone="amber" rows={m.overdueActions} onRow={goP}
          hint="À relancer auprès des responsables (R)."
          render={r => <Row reference={r.problem.reference} title={r.title}
            meta={<><span className="row-who">{r.responsibles.join(', ') || '—'}</span><span className="row-date is-late">{dateFr(r.dueDate)}</span></>} />} />
        <List title="Erreurs connues sans action corrective" tone="amber" rows={m.knownErrorsNoAction} onRow={goP}
          hint="Une erreur connue doit porter au moins une action planifiée."
          render={r => <Row reference={r.reference} title={r.title}
            meta={<span className={'badge ' + r.priority}>{r.priority}</span>} />} />
        <List title="Revues échues" tone="amber" rows={m.reviewsDue} onRow={goItem}
          hint="Articles publiés et SLA en vigueur dont la date de revue est passée." render={itemRow} />
      </Group>}

      <Group id="travail" title="Mon travail" total={work}>
        <List title="Mes actions en cours" tone="emerald" rows={myTodo} onRow={goP}
          render={r => <Row reference={r.problem.reference} title={r.title}
            meta={<><Roles roles={r.myRoles} /><span className="row-status">{r.status}</span>{r.dueDate && <span className="row-date">{dateFr(r.dueDate)}</span>}</>} />} />
        <List title="Mes dossiers ouverts" tone="emerald" rows={c.myAssignments} onRow={goItem}
          hint="Incidents, demandes, changements et améliorations dont vous êtes responsable." render={itemRow} />
      </Group>

      <Group id="suivi" title="À suivre" total={follow}>
        <div className="c-follow">
          <List title="Mes problèmes déclarés" tone="neutral" rows={c.myProblems} onRow={goP}
            render={r => <Row reference={r.reference} title={r.title}
              meta={<span className={'badge ' + badge(r.status)}>{r.status}</span>} />} />
          {m && <List title="Analyses sans cause racine" tone="neutral" rows={m.inAnalysisNoRootCause} onRow={goP}
            render={r => <Row reference={r.reference} title={r.title}
              meta={<span className="row-who">{r.analysesCount} analyse{r.analysesCount > 1 ? 's' : ''}</span>} />} />}
        </div>
      </Group>

      <p className="c-foot">Indicateurs et volumétrie : <Link to="/reports">Reporting</Link></p>
    </>
  )
}
