import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { incidentConfig } from '../config'
import { useServiceConfig } from '../../../core/useServices'
import { useCategoryConfig } from '../../../core/useCategories'

export default function Incidents() {
  const config = useCategoryConfig(useServiceConfig(incidentConfig), 'incident')
  return config ? <RecordList config={config} /> : <p>Chargement…</p>
}
