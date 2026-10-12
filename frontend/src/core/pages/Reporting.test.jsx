import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Reporting, { periodBounds } from './Reporting'
import { toCsv } from './reporting/exportCsv'

const overview = {
  period: { from: '2026-09-13', to: '2026-10-12' },
  health: { score: 75, level: 'Dégradé', factors: [
    { label: 'Incidents majeurs ouverts', count: 1, penalty: 25, rule: '−25 chacun, 50 au plus' }] },
  kpis: { majorIncidentsOpen: 1, slaRate: 92.5, slaMeasured: 40, changesThisWeek: 3, projectsAtRisk: 0 },
  projectsAtRisk: [],
  volume: [{ date: '2026-10-11T00:00:00Z', incidents: 2, requests: 1, changes: 0 },
           { date: '2026-10-12T00:00:00Z', incidents: 4, requests: 3, changes: 1 }],
  priorityMatrix: [{ level: 'Critique', incidents: 1, problems: 0 }, { level: 'Élevée', incidents: 2, problems: 1 },
                   { level: 'Moyenne', incidents: 0, problems: 0 }, { level: 'Basse', incidents: 0, problems: 0 }],
  badges: { incidents: { count: 3, tone: 'bad' }, problems: { count: 0, tone: 'wait' },
            changesProjects: { count: 0, tone: 'wait' }, compliance: { count: 1, tone: 'wait' } }
}
const incidents = {
  kpis: { mttrHours: 5.5, mttdHours: null, mttdMeasured: 0, fcr: 50, resolved: 2, open: 1 },
  backlog: [{ bucket: '< 24 h', count: 1 }, { bucket: '1 à 7 j', count: 0 }, { bucket: '> 7 j', count: 0 }],
  burnUp: [{ date: '2026-10-12T00:00:00Z', created: 3, resolved: 2 }],
  byCategory: [], byService: [],
  watch: [{ id: 9, reference: 'INC-2026-0009', title: 'Paie arrêtée', priority: 'P1', isMajor: true, status: 'Nouveau',
            ownerDisplayName: 'Hery', resolutionDueAt: null, sla: 'Sans SLA', affectedService: 'Paie' },
          { id: 10, reference: 'INC-2026-0010', title: 'Messagerie lente', priority: 'P3', isMajor: false, status: 'En cours',
            ownerDisplayName: 'Hery', resolutionDueAt: '2026-10-12T10:00:00Z', sla: 'SLA dépassé', affectedService: 'Messagerie' }]
}

const dashboard = vi.fn((tab) => Promise.resolve(tab === 'overview' ? overview : incidents))
vi.mock('../../api', () => ({
  api: {
    services: { list: () => Promise.resolve([{ title: 'Paie' }, { title: 'Messagerie' }]) },
    reports: { dashboard: (...a) => dashboard(...a) }
  }
}))

afterEach(() => { cleanup(); dashboard.mockClear() })

const renderAt = (url) => render(<MemoryRouter initialEntries={[url]}><Reporting /></MemoryRouter>)

it('ouvre sur la synthèse : score de santé, alerte des incidents majeurs, badges des onglets', async () => {
  renderAt('/reports')
  expect(await screen.findByText('75')).toBeTruthy()
  expect(screen.getByText('Alerte : à traiter en priorité')).toBeTruthy()
  const tabs = screen.getAllByRole('tab').map(t => t.textContent)
  expect(tabs).toEqual(['Synthèse globale', 'Incidents3', 'Problèmes', 'Changements & projets', 'Conformité & métriques1'])
  expect(screen.getByLabelText('3 alerte(s)').className).toContain('bad')
})

it('passe la période et le service à l\'API, et filtre les incidents à suivre', async () => {
  renderAt('/reports?tab=incidents&period=7&service=Paie')
  expect(await screen.findByText('Paie arrêtée')).toBeTruthy()
  const [tab, params] = dashboard.mock.calls.find(c => c[0] === 'incidents')
  expect(tab).toBe('incidents')
  expect(params.service).toBe('Paie')
  expect((new Date(params.to) - new Date(params.from)) / 86400000).toBe(6)

  fireEvent.change(screen.getByLabelText('Filtrer les incidents à suivre'), { target: { value: 'SLA dépassé' } })
  const table = screen.getByRole('table')
  expect(within(table).queryByText('Paie arrêtée')).toBeNull()
  expect(within(table).getByText('Messagerie lente')).toBeTruthy()
})

it('période personnalisée : bornes conservées ; sinon, N jours jusqu\'à aujourd\'hui inclus', () => {
  expect(periodBounds('custom', '2026-01-01', '2026-03-31')).toEqual({ from: '2026-01-01', to: '2026-03-31' })
  expect(periodBounds('30', null, null, new Date(2026, 9, 12))).toEqual({ from: '2026-09-13', to: '2026-10-12' })
})

it('CSV pour Excel : séparateur « ; », décimales à la française, champs protégés', () => {
  const csv = toCsv([{ title: 'Indicateurs', header: ['Indicateur', 'Valeur'], rows: [['MTTR; heures', 5.5], ['Vide', null]] }])
  expect(csv).toBe('Indicateurs\r\nIndicateur;Valeur\r\n"MTTR; heures";5,5\r\nVide;')
})
