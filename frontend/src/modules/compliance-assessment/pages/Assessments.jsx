import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { assessmentConfig } from '../config'

export default function Assessments() {
  return <RecordList config={assessmentConfig} />
}
