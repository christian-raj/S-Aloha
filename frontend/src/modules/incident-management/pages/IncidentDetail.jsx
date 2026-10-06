import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { incidentConfig } from '../config'

/** Un incident récurrent ou de cause inconnue ouvre un problème, relié à l'incident. */
function OpenProblem({ incident }) {
  const [error, setError] = useState('')
  const nav = useNavigate()
  const open = async () => {
    try {
      const p = await api.problems.create({
        title: incident.title, description: incident.description, impact: incident.impact,
        urgency: incident.urgency, category: incident.category, affectedService: incident.affectedService
      })
      await api.links.create({ fromType: 'incident', fromId: incident.id, toReference: p.reference })
      nav('/problems/' + p.id)
    } catch (e) { setError(e.message) }
  }
  return (
    <div className="card" style={{ marginTop: 16 }}>
      <h3 style={{ color: 'var(--navy)', marginBottom: 4 }}>Cause sous-jacente</h3>
      <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 10 }}>
        Incident récurrent ou cause inconnue : ouvrir un problème pour en rechercher la cause racine.</p>
      {error && <div className="error">{error}</div>}
      <button className="btn ghost" onClick={open}>Ouvrir un problème lié</button>
    </div>
  )
}

export default function IncidentDetail() {
  return <RecordDetail config={incidentConfig}>{r => <OpenProblem incident={r} />}</RecordDetail>
}
