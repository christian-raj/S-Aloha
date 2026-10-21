import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, getUser } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { hrefOf } from '../../../core/itemTypes'
import useRequestConfig from '../useRequestConfig'

/**
 * REQ-12 : une demande qui révèle un besoin non standard se requalifie en changement (ou en
 * incident) relié ; la demande est annulée avec ce motif. Demandeur ou gestionnaire, avant
 * le traitement.
 */
export function Requalify({ request }) {
  const [target, setTarget] = useState('change')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const nav = useNavigate()
  const user = getUser()
  const allowed = ['Admin', 'Manager'].includes(user?.role) || user?.username?.toLowerCase() === request.createdBy?.toLowerCase()
  if (!allowed || !['Soumise', 'Approuvée'].includes(request.status)) return null
  const go = () => api.requests.requalify(request.id, { target, reason })
    .then(r => nav(hrefOf(r.target.type, r.target.id))).catch(e => setError(e.message))
  return (
    <section className="card" style={{ marginTop: 16 }} aria-label="Requalifier la demande">
      <h3 className="card-title">Besoin non standard</h3>
      <p className="muted small" style={{ marginBottom: 8 }}>
        La demande ne relève pas du catalogue : la requalifier crée un changement (ou un incident) relié, et annule la demande avec ce motif.
      </p>
      {error && <div className="error" role="alert">{error}</div>}
      <div className="toolbar">
        <select value={target} onChange={e => setTarget(e.target.value)} aria-label="Requalifier en" style={{ maxWidth: 200 }}>
          <option value="change">En changement</option>
          <option value="incident">En incident</option>
        </select>
        <input value={reason} onChange={e => setReason(e.target.value)} placeholder="Motif, ex. : travaux d’infrastructure hors catalogue"
          aria-label="Motif de la requalification" style={{ flex: 1, minWidth: 200 }} />
        <button className="btn ghost" onClick={go} disabled={!reason.trim()}>Requalifier</button>
      </div>
    </section>
  )
}

export default function RequestDetail() {
  const config = useRequestConfig()
  return config ? <RecordDetail config={config}>{r => <Requalify request={r} />}</RecordDetail> : <p>Chargement…</p>
}
