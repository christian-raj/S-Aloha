import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { incidentConfig } from '../config'

export default function Incidents() {
  return <RecordList config={incidentConfig} />
}
