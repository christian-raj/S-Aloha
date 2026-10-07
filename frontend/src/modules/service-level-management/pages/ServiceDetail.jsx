import React from 'react'
import { Link } from 'react-router-dom'
import RecordDetail from '../../../core/components/RecordDetail'
import { dateFr } from '../../../core/fields'
import { serviceConfig } from '../config'

/** Accords de niveau de service rattachés au service. */
function Agreements({ service }) {
  return (
    <div className="card">
      <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Accords de niveau de service ({service.agreements.length})</h3>
      {service.agreements.length === 0
        ? <p style={{ color: 'var(--muted)', fontSize: 13 }}>Aucun SLA pour ce service. <Link to="/agreements">Créer un SLA →</Link></p>
        : <table><thead><tr><th>Référence</th><th>Intitulé</th><th>Client</th><th>Disponibilité</th><th>Revue</th><th>Statut</th></tr></thead>
          <tbody>{service.agreements.map(a => (
            <tr key={a.id}>
              <td><Link to={'/agreements/' + a.id}><b>{a.reference}</b></Link></td><td>{a.title}</td>
              <td>{a.customer || '—'}</td><td>{a.availabilityTarget != null ? a.availabilityTarget + ' %' : '—'}</td>
              <td>{dateFr(a.reviewDate)}</td><td>{a.status}</td>
            </tr>))}</tbody></table>}
    </div>
  )
}

export default function ServiceDetail() {
  return <RecordDetail config={serviceConfig} tab={{ label: 'Accords (SLA)', render: r => <Agreements service={r} /> }} />
}
