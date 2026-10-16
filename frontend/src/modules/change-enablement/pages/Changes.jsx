import React from 'react'
import RecordList from '../../../core/components/RecordList'
import useChangeConfig from '../useChangeConfig'

export default function Changes() {
  const config = useChangeConfig()
  return config ? <RecordList config={config} /> : <p>Chargement…</p>
}
