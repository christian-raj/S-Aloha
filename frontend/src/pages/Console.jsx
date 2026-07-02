import React, { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api'

const badge = s => s.replace(/[ é]/g, m => (m === ' ' ? '' : 'e'))
const dateFr = d => new Date(d).toLocaleDateString('fr-FR')

/** Carte d'une zone "à traiter" : bord coloré + compteur, masquée si vide. */
function TodoSection({ title, tone, rows, cols, onRow, hint }) {
  if (rows.length === 0) return null
  return (
    <div className="card todo" style={{ borderLeft: `4px solid ${tone}`, marginBottom: 14 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 4 }}>
        {title} <span style={{ color: tone, fontWeight: 800 }}>({rows.length})</span>
      </h3>
      {hint && <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 10 }}>{hint}</p>}
      <table>
        <thead><tr>{cols.map(c => <th key={c.h}>{c.h}</th>)}</tr></thead>
        <tbody>{rows.map(r => (
          <tr key={r.id} className="clickable" onClick={() => onRow(r)}>
            {cols.map(c => <td key={c.h}>{c.v(r)}</td>)}
          </tr>))}
        </tbody>
      </table>
    </div>
  )
}

/** Console orientée action : ce qui requiert une intervention d'abord, le suivi ensuite. */
export default function Console() {
  const [c, setC] = useState(null)
  const [error, setError] = useState('')
  const nav = useNavigate()
  useEffect(() => { api.console.get().then(setC).catch(e => setError(e.message)) }, [])
  if (error) return <div className="error">{error}</div>
  if (!c) return <p>Chargement…</p>

  const goP = r => nav('/problems/' + (r.problem ? r.problem.id : r.id))
  const m = c.management

  // --- Zone 1 : à traiter (par ordre de criticité) ---
  const myOverdue = c.myActions.filter(a => a.overdue)
  const myTodo = c.myActions.filter(a => !a.overdue)
  const nbTodo = myOverdue.length + myTodo.length
    + (m ? m.overdueActions.length + m.toQualify.length + m.knownErrorsNoAction.length : 0)

  const roleLabel = { Admin: 'administrateur', Manager: 'gestionnaire', User: 'utilisateur' }[c.role]

  return (
    <>
      <h1 className="page-title">Ma console</h1>
      <p className="page-sub">Console {roleLabel} — ce qui requiert votre intervention apparaît en premier.</p>

      {/* Bandeau de synthèse */}
      <div className="card" style={{
        marginBottom: 20, display: 'flex', alignItems: 'center', gap: 12,
        background: nbTodo > 0 ? '#fdeeda' : '#ddf3e8',
        borderColor: nbTodo > 0 ? '#f0d5a8' : '#b8e4cd'
      }}>
        <span style={{ fontSize: 26 }}>{nbTodo > 0 ? '⚠' : '✓'}</span>
        <div>
          <b style={{ color: 'var(--navy)' }}>
            {nbTodo > 0 ? `${nbTodo} élément${nbTodo > 1 ? 's' : ''} à traiter` : 'Rien à traiter'}
          </b>
          <div style={{ fontSize: 13, color: 'var(--muted)' }}>
            {nbTodo > 0 ? 'Les zones ci-dessous sont classées par criticité.' : 'Aucune intervention requise de votre part pour le moment.'}
          </div>
        </div>
      </div>

      {/* ---- Zone À TRAITER ---- */}
      <h2 style={{ color: 'var(--danger)', fontSize: 17, margin: '0 0 12px' }}>À traiter</h2>

      <TodoSection title="Mes actions en retard" tone="var(--danger)" rows={myOverdue} onRow={goP}
        hint="Échéance dépassée : à traiter ou replanifier immédiatement."
        cols={[
          { h: 'Action', v: r => <b>{r.title}</b> },
          { h: 'Mon rôle', v: r => r.myRoles.map(x => <span key={x} className={'badge ' + x} style={{ marginRight: 4 }}>{x}</span>) },
          { h: 'Problème', v: r => r.problem.reference },
          { h: 'Échéance', v: r => <span style={{ color: 'var(--danger)', fontWeight: 700 }}>{dateFr(r.dueDate)} ⚠</span> }
        ]} />

      {m && <TodoSection title="Problèmes à qualifier" tone="var(--blue)" rows={m.toQualify} onRow={goP}
        hint="Nouveaux problèmes en attente de qualification par un gestionnaire."
        cols={[
          { h: 'Référence', v: r => <b>{r.reference}</b> },
          { h: 'Titre', v: r => r.title },
          { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> },
          { h: 'Déclaré par', v: r => r.createdByDisplayName },
          { h: 'Le', v: r => dateFr(r.createdAt) }
        ]} />}

      {m && <TodoSection title="Erreurs connues sans action corrective" tone="var(--warn)" rows={m.knownErrorsNoAction} onRow={goP}
        hint="Une erreur connue doit porter au moins une action corrective planifiée."
        cols={[
          { h: 'Référence', v: r => <b>{r.reference}</b> },
          { h: 'Titre', v: r => r.title },
          { h: 'Priorité', v: r => <span className={'badge ' + r.priority}>{r.priority}</span> }
        ]} />}

      {m && <TodoSection title="Actions en retard (toutes équipes)" tone="var(--warn)" rows={m.overdueActions} onRow={goP}
        hint="À relancer auprès des responsables (R)."
        cols={[
          { h: 'Action', v: r => <b>{r.title}</b> },
          { h: 'Problème', v: r => r.problem.reference },
          { h: 'Échéance', v: r => <span style={{ color: 'var(--danger)', fontWeight: 700 }}>{dateFr(r.dueDate)}</span> },
          { h: 'Responsables', v: r => r.responsibles.join(', ') || '—' }
        ]} />}

      <TodoSection title="Mes actions en cours" tone="var(--blue)" rows={myTodo} onRow={goP}
        cols={[
          { h: 'Action', v: r => <b>{r.title}</b> },
          { h: 'Mon rôle', v: r => r.myRoles.map(x => <span key={x} className={'badge ' + x} style={{ marginRight: 4 }}>{x}</span>) },
          { h: 'Problème', v: r => r.problem.reference },
          { h: 'Échéance', v: r => r.dueDate ? dateFr(r.dueDate) : '—' },
          { h: 'Statut', v: r => r.status }
        ]} />

      {nbTodo === 0 && <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 20 }}>Aucun élément à traiter. 👍</p>}

      {/* ---- Zone À SUIVRE (informatif, ton neutre) ---- */}
      <h2 style={{ color: 'var(--muted)', fontSize: 17, margin: '26px 0 12px' }}>À suivre</h2>

      <div className="card" style={{ marginBottom: 14, opacity: .93 }}>
        <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Mes problèmes déclarés (ouverts) <span style={{ color: 'var(--muted)', fontWeight: 600 }}>({c.myProblems.length})</span></h3>
        {c.myProblems.length === 0
          ? <p style={{ color: 'var(--muted)', fontSize: 13 }}>Aucun problème ouvert déclaré par vous.</p>
          : <table><thead><tr><th>Référence</th><th>Titre</th><th>Statut</th><th>Priorité</th><th>Déclaré le</th></tr></thead>
            <tbody>{c.myProblems.map(r => (
              <tr key={r.id} className="clickable" onClick={() => goP(r)}>
                <td><b>{r.reference}</b></td><td>{r.title}</td>
                <td><span className={'badge ' + badge(r.status)}>{r.status}</span></td>
                <td><span className={'badge ' + r.priority}>{r.priority}</span></td>
                <td>{dateFr(r.createdAt)}</td>
              </tr>))}</tbody></table>}
      </div>

      {m && (
        <div className="card" style={{ marginBottom: 14, opacity: .93 }}>
          <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Analyses en cours sans cause racine <span style={{ color: 'var(--muted)', fontWeight: 600 }}>({m.inAnalysisNoRootCause.length})</span></h3>
          {m.inAnalysisNoRootCause.length === 0
            ? <p style={{ color: 'var(--muted)', fontSize: 13 }}>Toutes les analyses en cours ont une cause racine identifiée.</p>
            : <table><thead><tr><th>Référence</th><th>Titre</th><th>Priorité</th><th>Analyses</th></tr></thead>
              <tbody>{m.inAnalysisNoRootCause.map(r => (
                <tr key={r.id} className="clickable" onClick={() => goP(r)}>
                  <td><b>{r.reference}</b></td><td>{r.title}</td>
                  <td><span className={'badge ' + r.priority}>{r.priority}</span></td>
                  <td>{r.analysesCount}</td>
                </tr>))}</tbody></table>}
        </div>
      )}

      <p style={{ marginTop: 8 }}>
        Les indicateurs et la volumétrie de l'application sont dans le <Link to="/reports">Reporting →</Link>
      </p>
    </>
  )
}
