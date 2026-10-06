import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import RecordDetail from '../../../core/components/RecordDetail'
import { changeConfig } from '../config'

/** Conflits de calendrier : autres changements sur le même CI, créneaux qui se chevauchent. */
function Conflicts({ change }) {
  const [conflicts, setConflicts] = useState([])
  useEffect(() => { api.changes.conflicts(change.id).then(setConflicts).catch(() => setConflicts([])) }, [change])
  if (conflicts.length === 0) return null
  return (
    <div className="error" style={{ marginTop: 16 }}>
      <b>Conflit de calendrier</b> — ce changement chevauche :
      <ul style={{ margin: '6px 0 0 18px' }}>
        {conflicts.map(c => (
          <li key={c.changeId + '-' + c.ciId}>
            <Link to={'/changes/' + c.changeId}><b>{c.changeReference}</b> {c.changeTitle}</Link>
            {' '}sur <Link to={'/configuration/' + c.ciId}>{c.ciReference} {c.ciTitle}</Link>
          </li>
        ))}
      </ul>
    </div>
  )
}

export default function ChangeDetail() {
  return <RecordDetail config={changeConfig}>{r => <Conflicts change={r} />}</RecordDetail>
}
