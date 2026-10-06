import React from 'react'
import RecordDetail from '../../../core/components/RecordDetail'
import useAgreementConfig from './useAgreementConfig'

export default function AgreementDetail() {
  const config = useAgreementConfig()
  return config ? <RecordDetail config={config} /> : <p>Chargement…</p>
}
