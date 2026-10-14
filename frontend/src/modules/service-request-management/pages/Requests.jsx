import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { requestConfig } from '../config'
import { useServiceConfig } from '../../../core/useServices'

export default function Requests() {
  const config = useServiceConfig(requestConfig, 'requestedItem')
  return config ? <RecordList config={config} /> : <p>Chargement…</p>
}
