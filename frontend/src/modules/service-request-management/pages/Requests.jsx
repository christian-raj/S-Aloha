import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { requestConfig } from '../config'

export default function Requests() {
  return <RecordList config={requestConfig} />
}
