import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Console from './Console'

const item = (type, id, reference, title, status) => ({ type, id, reference, title, status })
const data = {
  role: 'Manager', username: 'chef', myProblems: [], myActions: [], mySlaIncidents: [],
  myAssignments: [item('project', 4, 'PRJ-2026-0004', 'Portail RH', 'En cours')],
  myProjectAlerts: [item('project', 4, 'PRJ-2026-0004', 'Portail RH', '1 jalon(s) en retard')],
  management: {
    toQualify: [], inAnalysisNoRootCause: [], knownErrorsNoAction: [], overdueActions: [], majorIncidents: [], slaIncidents: [],
    decisions: [item('project', 5, 'PRJ-2026-0005', 'Refonte intranet', 'À valider')], reviewsDue: [],
    overdueRequests: [item('request', 2, 'REQ-2026-0002', 'Badge', 'Approuvée')],
    projectAlerts: [item('project', 4, 'PRJ-2026-0004', 'Portail RH', 'délai hors tolérance')]
  }
}
vi.mock('../../api', () => ({
  getUser: () => ({ username: 'chef', displayName: 'Chef Projet', role: 'Manager' }),
  api: { console: { get: () => Promise.resolve(data) } }
}))
vi.mock('../usePerimeters', () => ({ default: () => [] }))

afterEach(cleanup)

describe('Console : projets et demandes en retard (PRJ-34, REQ-30)', () => {
  it('montre projets à valider, projets et demandes à relancer, mes projets à relancer', async () => {
    render(<MemoryRouter><Console /></MemoryRouter>)
    expect(await screen.findByText('Projets à relancer')).toBeTruthy()
    expect(screen.getByText('Demandes en retard')).toBeTruthy()
    expect(screen.getByText('Mes projets à relancer')).toBeTruthy()
    expect(screen.getByText('À valider')).toBeTruthy()
    expect(screen.getByText('délai hors tolérance')).toBeTruthy()
    expect(screen.getByText('1 jalon(s) en retard')).toBeTruthy()
  })
})
