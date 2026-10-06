import React from 'react'
import RecordList from '../../../core/components/RecordList'
import useAgreementConfig from './useAgreementConfig'

export default function Agreements() {
  const config = useAgreementConfig()
  return config ? <RecordList config={config} /> : <p>Chargement…</p>
}
