import React from 'react'
import { afterEach, expect, it } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import ProblemsTab, { problemsCsv } from './reporting/ProblemsTab'

afterEach(cleanup)

const data = {
  kpis: { open: 3, knownErrors: 1, incidentsPerProblem: 2, rcaPending: 0, rootCauseRate: 75, recurrences: 1 },
  topServices: [], funnel: [{ stage: 'Identifié', count: 1 }],
  actions: { open: 0, overdue: 0, done: 0, overdueList: [] },
  topProblems: [{ id: 4, reference: 'PRB-2026-0004', title: 'Certificat expiré', status: 'Résolu', incidents: 6 }],
}

it('PRB-33 à PRB-35 : cause racine, récurrences en alerte, problèmes aux incidents les plus nombreux', () => {
  render(<MemoryRouter><ProblemsTab data={data} /></MemoryRouter>)
  expect(screen.getByText('Problèmes clos avec cause racine')).toBeTruthy()
  expect(screen.getByText('À revoir : le correctif ne tient pas')).toBeTruthy()
  expect(screen.getByRole('link', { name: 'PRB-2026-0004' }).getAttribute('href')).toBe('/problems/4')
  expect(problemsCsv(data).map(s => s.title)).toContain('Problèmes aux incidents les plus nombreux')
})
