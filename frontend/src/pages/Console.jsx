import React, { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api'

const badge = s => s.replace(/[ é]/g, m => (m === ' ' ? '' : 'e'))
const dateFr = d => new Date(d).toLocaleDateString('fr-FR')

function Section({ title, count, children, tone }) {
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <h3 style={{ color: tone || 'var(--navy)', marginBottom: 12 }}>
        {title} {count !== undefined && <span style={{ color: 'var(--muted)', fontWeight: 600 }}>({count})</span>}
      </h3>
      {children}
    </div>
  )
}

function MiniTable({ rows, cols, onRow, empty }) {
  if (rows.length === 0) return <p style={{ color: 'var(--muted)', fontSize: 13 }}>{empty}</p>
  return (
    <table>
      <thead><tr>{cols.map(c => <th key={c.h}>{c.h}</th>)}</tr></thead>
      <tbody>{rows.map(r => (
        <tr key={r.id} className={onRow ? 'clickable' : ''} onClick={() => onRow && onRow(r)}>
          {cols.map(c => <td key={c.h}>{c.v(r)}</td>)}
        </tr>))}
      </tbody>
    </table>
  )
}

/** Console personnalisée : chaque rôle voit ses propres blocs. */
export default function Console() {
  const [c, setC] = useState(null)
  const [error, setError] = useState('')
  const nav = useNavigate()
  useEffect(() => { api.console.get().then(setC).catch(e => setError(e.message)) }, [])
  if (error) return <div className="error">{error}</div>
  if (!c) return <p>Chargement…</p>

  const goP = r => nav('/problems/' + (r.problem ? r.problem.id : r.id))

  return (
    <>
      <h1 className="page-title">Ma console</h1>
      <p className="page-sub">
        {c.role === 'Admin' && 'Console administrateur : votre activité, le pilotage du processus et la santé de l\u0027application.'}
        {c.role === 'Manager' && 'Console gestionnaire : votre activité et le pilotage du processus de gestion des problèmes.'}
        {c.role === 'User' && 'Votre activité : problèmes déclarés et actions qui vous sont affectées.'}
      </p>

      {/* ---- Bloc personnel : tous les rôles ---- */}
      <Section title="Mes actions à traiter" count={c.myActions.length}>
        <MiniTable rows={c.myActions} onRow={goP}
          empty="Aucune action ouverte ne vous est affectée."
          cols={[
            { h: 'Action', v: r => <b>{r.title}</b> },
            { h: 'Mon rôle', v: r => r.myRoles.map(x => <span key={x} className={'badge ' + x} style={{ marginRight: 4 }}>{x}</span>) },
            { h: 'Problème', v: r => r.problem.reference },
            { h: 'Échéance', v: r => r.dueDate
                ? <span style={r.overdue ? { color: 'var(--danger)', fontWeight: 700 } : {}}>{dateFr(r.dueDate)}{r.overdue && ' ⚠'}</span> : '—' },
            { h: 'Statut', v: r => r.status }
          ]} />
      </Section>

      <Section title="Mes problèmes déclarés (ouverts)" count={c.myProblems.length}>
        <MiniTable rows={c.myProblems} onRow={goP}
          empty="Aucun problème ouvert déclaré par vous."
          cols={[
            { h: 'Référence', v: r => <b>{r.reference}</b> },
            { h: 'Titre', v: r => r.title },
            { h: 'Statut', v: r => <span className={'badge ' + badge(r.status)}>{r.status}</span> },
            { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> },
            { h: 'Déclaré le', v: r => dateFr(r.createdAt) }
          ]} />
      </Section>

      {/* ---- Bloc pilotage : Manager et Admin ---- */}
      {c.management && (
        <>
          <h2 style={{ color: 'var(--blue)', fontSize: 18, margin: '26px 0 12px' }}>Pilotage du processus</h2>

          <Section title="Problèmes à qualifier" count={c.management.toQualify.length} tone="var(--blue)">
            <MiniTable rows={c.management.toQualify} onRow={goP}
              empty="Aucun nouveau problème en attente de qualification."
              cols={[
                { h: 'Référence', v: r => <b>{r.reference}</b> },
                { h: 'Titre', v: r => r.title },
                { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> },
                { h: 'Déclaré par', v: r => r.createdByDisplayName },
                { h: 'Le', v: r => dateFr(r.createdAt) }
              ]} />
          </Section>

          <Section title="Analyses en cours sans cause racine" count={c.management.inAnalysisNoRootCause.length} tone="var(--blue)">
            <MiniTable rows={c.management.inAnalysisNoRootCause} onRow={goP}
              empty="Toutes les analyses en cours ont une cause racine identifiée."
              cols={[
                { h: 'Référence', v: r => <b>{r.reference}</b> },
                { h: 'Titre', v: r => r.title },
                { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> },
                { h: 'Analyses', v: r => r.analysesCount }
              ]} />
          </Section>

          <Section title="Erreurs connues sans action corrective" count={c.management.knownErrorsNoAction.length} tone="var(--warn)">
            <MiniTable rows={c.management.knownErrorsNoAction} onRow={goP}
              empty="Toutes les erreurs connues ont au moins une action corrective."
              cols={[
                { h: 'Référence', v: r => <b>{r.reference}</b> },
                { h: 'Titre', v: r => r.title },
                { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> }
              ]} />
          </Section>

          <Section title="Actions en retard (toutes équipes)" count={c.management.overdueActions.length} tone="var(--danger)">
            <MiniTable rows={c.management.overdueActions} onRow={goP}
              empty="Aucune action en retard."
              cols={[
                { h: 'Action', v: r => <b>{r.title}</b> },
                { h: 'Problème', v: r => r.problem.reference },
                { h: 'Échéance', v: r => <span style={{ color: 'var(--danger)', fontWeight: 700 }}>{dateFr(r.dueDate)}</span> },
                { h: 'Responsables', v: r => r.responsibles.join(', ') || '—' }
              ]} />
          </Section>
        </>
      )}

      {/* ---- Bloc administration : Admin uniquement ---- */}
      {c.administration && (
        <>
          <h2 style={{ color: 'var(--navy)', fontSize: 18, margin: '26px 0 12px' }}>Administration</h2>
          <div className="kpi-row">
            <div className="kpi"><div className="n">{c.administration.totalProblems}</div><div className="l">Problèmes (total)</div></div>
            <div className="kpi"><div className="n">{c.administration.totalActions}</div><div className="l">Actions (total)</div></div>
            <div className="kpi"><div className="n">{c.administration.totalAnalyses}</div><div className="l">Analyses RCA</div></div>
            <div className="kpi"><div className="n">{c.administration.contributors}</div><div className="l">Déclarants distincts</div></div>
          </div>
          <p style={{ color: 'var(--muted)', fontSize: 13 }}>
            Dernière activité : {c.administration.lastActivity ? dateFr(c.administration.lastActivity) : '—'}.
            Le paramétrage des groupes AD et des rôles se fait dans <code>appsettings.json</code> (voir docs/rules.md).
          </p>
        </>
      )}

      <p style={{ marginTop: 8 }}><Link to="/reports">Voir le reporting global →</Link></p>
    </>
  )
}
