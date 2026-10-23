import React from 'react'
import { Link } from 'react-router-dom'
import RecordDetail from '../../../core/components/RecordDetail'
import { dateTimeFr } from '../../../core/fields'
import { declarationConfig } from '../config'

/** DRG-10, DRG-11 : échéances légales de la déclaration, envois et retards. */
function Steps({ r }) {
  return (
    <section className="card" style={{ marginTop: 16 }}>
      <h3 className="card-title">Échéances</h3>
      <p className="muted small" style={{ marginBottom: 8 }}>
        Incident <Link to={'/incidents/' + r.incidentId}><b>{r.incidentReference}</b> {r.incidentTitle}</Link>
      </p>
      <div className="table-scroll">
        <table className="compact">
          <thead><tr><th>Étape</th><th>Échéance</th><th>Envoyée le</th><th></th></tr></thead>
          <tbody>
            {r.steps.map(s => (
              <tr key={s.step}>
                <td><b>{s.step}</b></td>
                <td>{dateTimeFr(s.dueAt)}</td>
                <td>{s.sentAt ? dateTimeFr(s.sentAt) : <span className="muted">non envoyée</span>}</td>
                <td>{s.late && <span className="badge t-bad">{s.sentAt ? 'Envoyée en retard' : 'Échéance dépassée'}</span>}</td>
              </tr>))}
          </tbody>
        </table>
      </div>
      {r.sentVersions.length > 0 && <>
        <h4 className="form-heading" style={{ marginTop: 12 }}>Contenu envoyé</h4>
        {r.sentVersions.map(v => (
          <details key={v.step + v.sentAt}>
            <summary>{v.step} — {dateTimeFr(v.sentAt)}</summary>
            <dl className="small" style={{ margin: '6px 0 10px' }}>
              <dt>Résumé</dt><dd>{v.summary ?? '—'}</dd>
              <dt>Impact</dt><dd>{v.impact ?? '—'}</dd>
              <dt>Mesures prises</dt><dd>{v.measuresTaken ?? '—'}</dd>
            </dl>
          </details>))}
      </>}
    </section>
  )
}

/** DRG-08 : rubriques attendues par le régime, encore vides — à compléter avant l'envoi. */
export function Rubrics({ r }) {
  if (!r.missingRubrics?.length) return null
  return (
    <div className="notice warn" role="status" style={{ marginTop: 16 }}>
      <b>Contenu attendu ({r.regime}) :</b> {r.missingRubrics.join(', ')} — à compléter dans l’onglet Détails avant l’envoi à {r.authority}.
    </div>
  )
}

export default function DeclarationDetail() {
  return <RecordDetail config={declarationConfig}>{r => <><Rubrics r={r} /><Steps r={r} /></>}</RecordDetail>
}
