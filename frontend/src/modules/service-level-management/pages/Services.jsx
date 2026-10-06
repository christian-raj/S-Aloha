import React from 'react'
import RecordList from '../../../core/components/RecordList'
import { serviceConfig } from '../config'

export default function Services() {
  return <RecordList config={serviceConfig} />
}
