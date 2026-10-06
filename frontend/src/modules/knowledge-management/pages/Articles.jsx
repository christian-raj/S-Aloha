import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { knowledgeConfig } from '../config'

export default function Articles() {
  return <RecordList config={knowledgeConfig} />
}
