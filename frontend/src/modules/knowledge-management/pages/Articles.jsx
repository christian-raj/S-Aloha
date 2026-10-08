import React from 'react'
import RecordList from '../../../core/components/RecordList'
import KnowledgeSearch from '../../../core/components/KnowledgeSearch'
import { knowledgeConfig } from '../config'

export default function Articles() {
  return <RecordList config={knowledgeConfig} intro={<KnowledgeSearch />} />
}
