import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { ciConfig } from '../config'

export default function ConfigurationItems() {
  return <RecordList config={ciConfig} />
}
