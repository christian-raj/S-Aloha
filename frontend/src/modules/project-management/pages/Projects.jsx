import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { projectConfig } from '../config'

export default function Projects() {
  return <RecordList config={projectConfig} />
}
