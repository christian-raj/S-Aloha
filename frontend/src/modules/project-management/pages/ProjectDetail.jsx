import React from 'react'
import RecordDetail from '../../../core/components/RecordDetail'
import { projectConfig, componentsOf } from '../config'
import Overview, { BusinessCase } from '../components/Overview'
import { Backlog, Board, Sprints } from '../components/Agile'
import { Phases, Milestones, Deliverables, Risks, Issues } from '../components/Registers'

/**
 * Onglets d'un projet, selon ses composants (PRJ-03), dans un ordre stable :
 * structure (étapes, phases, jalons, livrables), travail Agile (backlog,
 * sprints, tableau), pilotage (business case et tolérances, risques, incidents).
 */
export const tabsFor = (r) => {
  const c = componentsOf(r)
  const has = (id) => c.includes(id)
  return [
    has('stages') && { id: 'stages', label: 'Étapes', render: (p, reload) => <Phases project={p} stages onChanged={reload} /> },
    has('phases') && { id: 'phases', label: 'Phases', render: (p, reload) => <Phases project={p} onChanged={reload} /> },
    has('milestones') && { id: 'milestones', label: 'Jalons', render: (p, reload) => <Milestones project={p} onChanged={reload} /> },
    has('deliverables') && { id: 'deliverables', label: 'Livrables', render: (p, reload) => <Deliverables project={p} onChanged={reload} /> },
    has('backlog') && { id: 'backlog', label: 'Backlog', render: (p, reload) => <Backlog project={p} onChanged={reload} /> },
    has('sprints') && { id: 'sprints', label: has('burndown') ? 'Sprints et burndown' : 'Sprints', render: (p, reload) => <Sprints project={p} onChanged={reload} /> },
    has('board') && { id: 'board', label: 'Tableau', render: (p, reload) => <Board project={p} onChanged={reload} /> },
    (has('business-case') || has('tolerances')) && { id: 'case', label: has('business-case') ? 'Business case' : 'Tolérances',
      render: (p, reload) => <BusinessCase key={p.updatedAt} project={p} onChanged={reload} /> },
    has('risks') && { id: 'risks', label: 'Risques', render: (p, reload) => <Risks project={p} onChanged={reload} /> },
    has('issues') && { id: 'issues', label: 'Incidents projet', render: (p, reload) => <Issues project={p} onChanged={reload} /> },
  ].filter(Boolean)
}

export default function ProjectDetail() {
  return (
    <RecordDetail config={projectConfig} tab={tabsFor}>
      {(r, reload) => <Overview project={r} onChanged={reload} />}
    </RecordDetail>
  )
}
