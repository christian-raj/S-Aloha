import React from 'react'
import RecordList from '../../../core/components/RecordList'
import useRequestConfig from '../useRequestConfig'

export default function Requests() {
  const config = useRequestConfig()
  return config ? <RecordList config={config} /> : <p>Chargement…</p>
}
