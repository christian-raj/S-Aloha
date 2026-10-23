import React from 'react'
import { afterEach, expect, it } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import ComplianceTab, { complianceCsv } from './reporting/ComplianceTab'

afterEach(cleanup)

const data = {
  nis2: { id: 2, reference: 'EVA-2026-0002', title: 'NIS2 2026', percent: 80, maturity: { level: 3, name: 'Défini' }, pillars: [], gaps: 4, improvements: 2,
    gapsTreated: 2, gapsTreatedRate: 50, delta: 5.5, previous: { id: 1, reference: 'EVA-2025-0001', percent: 74.5 } },
  nis2Pending: [], regulatory: [{ regime: 'NIS 2', total: 1, declared: 1, inProgress: 0, notRequired: 0, steps: [] }],
  qualificationHours: 3,
  availability: [{ id: 5, reference: 'SLA-2026-0005', title: 'Messagerie', service: 'Messagerie', percent: 99.2, target: 99.9, met: false, downtimeHours: 6, majorIncidents: 1 }],
  sla: { byService: [], toReview: [] },
  improvements: { byStatus: [], completed: 1, completedBySource: [{ label: 'Incident', count: 1 }], completionDays: 10, abandonRate: 50 },
  knowledge: { published: 1, drafts: 0, reviewOverdue: 0, publishedInPeriod: 1, upToDate: 100, incidentsWithArticle: 33.3,
    topUsed: [{ id: 3, reference: 'KB-2026-0003', title: 'Spouleur', uses: 4 }] },
}

it('NIS-30/31, SLM-31, CSI-31/32, DRG-21/22, KB-30 à 32 : indicateurs de l’onglet Conformité', () => {
  render(<MemoryRouter><ComplianceTab data={data} /></MemoryRouter>)
  expect(screen.getByText('Écarts traités')).toBeTruthy()
  expect(screen.getByRole('link', { name: 'EVA-2025-0001' })).toBeTruthy()
  expect(screen.getByText('Disponibilité des services sous SLA')).toBeTruthy()
  expect(screen.getByText('Taux d’abandon des améliorations')).toBeTruthy()
  expect(screen.getByText('Délai moyen de qualification')).toBeTruthy()
  expect(screen.getByText('KB-2026-0003 Spouleur')).toBeTruthy()
  expect(complianceCsv(data).map(s => s.title)).toEqual(expect.arrayContaining(['Déclarations par issue', 'Disponibilité des services sous SLA', 'Articles les plus utilisés']))
})
