import React from 'react'
import RecordDetail from '../../../core/components/RecordDetail'
import { requestConfig } from '../config'
import { useServiceConfig } from '../../../core/useServices'

export default function RequestDetail() {
  const config = useServiceConfig(requestConfig, 'requestedItem')
  return config ? <RecordDetail config={config} /> : <p>Chargement…</p>
}
