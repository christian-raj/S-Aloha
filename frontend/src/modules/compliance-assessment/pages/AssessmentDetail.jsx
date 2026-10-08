import React from 'react'
import RecordDetail from '../../../core/components/RecordDetail'
import { assessmentConfig } from '../config'
import Questionnaire from '../components/Questionnaire'
import Synthesis from '../components/Synthesis'

export default function AssessmentDetail() {
  return (
    <RecordDetail config={assessmentConfig} tab={[
      { label: 'Questionnaire', render: (r, reload) => <Questionnaire assessment={r} onChanged={reload} /> },
      { label: 'Synthèse', render: (r, reload) => <Synthesis assessment={r} onChanged={reload} /> },
    ]} />
  )
}
