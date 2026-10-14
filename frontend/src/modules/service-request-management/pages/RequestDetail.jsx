import React from 'react'
import RecordDetail from '../../../core/components/RecordDetail'
import useRequestConfig from '../useRequestConfig'

export default function RequestDetail() {
  const config = useRequestConfig()
  return config ? <RecordDetail config={config} /> : <p>Chargement…</p>
}
