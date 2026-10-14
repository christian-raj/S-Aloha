import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { incidentConfig } from '../config'
import { useServiceConfig } from '../../../core/useServices'

export default function Incidents() {
  const config = useServiceConfig(incidentConfig)
  return config ? <RecordList config={config} /> : <p>Chargement…</p>
}
