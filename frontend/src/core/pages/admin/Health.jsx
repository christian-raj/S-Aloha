import React, { useEffect, useState } from 'react'
import { api } from '../../../api'
import { dateTimeFr } from '../../fields'

const TONE = { OK: 't-done', 'Dégradé': 't-wait', 'Hors service': 't-bad' }

/** Administration → Santé (ADM-20, ADM-21) : état des composants et version déployée. */
export default function Health() {
  const [h, setH] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const load = () => { setBusy(true); api.admin.health().then(setH).catch(e => setError(e.message)).finally(() => setBusy(false)) }
  useEffect(() => { load() }, [])
  if (!h) return error ? <div className="error">{error}</div> : <p>Vérification en cours…</p>
  return (
    <>
      <h1 className="page-title">Santé</h1>
      <p className="page-sub">
        <span className={'badge ' + TONE[h.state]}>{h.state}</span>{' '}
        Version {h.version || 'inconnue'}{h.commit && <> ({h.commit})</>} · {h.environment} · démarrée le {dateTimeFr(h.startedAt)}
      </p>
      {error && <div className="error">{error}</div>}
      <div className="table-scroll">
        <table>
          <thead><tr><th>Composant</th><th>État</th><th>Détail</th><th>Durée</th></tr></thead>
          <tbody>{h.components.map(c => (
            <tr key={c.name}>
              <td><b>{c.name}</b></td>
              <td><span className={'badge ' + TONE[c.state]}>{c.state}</span></td>
              <td className="small">{c.message}</td>
              <td className="small mono">{c.durationMs ? `${Math.round(c.durationMs)} ms` : '—'}</td>
            </tr>))}
          </tbody>
        </table>
      </div>
      <p className="muted small" style={{ marginTop: 12 }}>Vérifié le {dateTimeFr(h.checkedAt)}. Les sondes publiques des orchestrateurs (Docker, Kubernetes) sont distinctes de cette page.</p>
      <button className="btn ghost" onClick={load} disabled={busy}>{busy ? 'Vérification…' : 'Vérifier à nouveau'}</button>
    </>
  )
}
