import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { changeConfig } from '../config'

export default function Changes() {
  return <RecordList config={changeConfig} />
}
