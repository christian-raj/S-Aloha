import React from 'react'
import { afterEach, expect, it } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import RequestsTab, { requestsCsv } from './reporting/RequestsTab'

afterEach(cleanup)

const data = {
  kpis: { fulfillmentHours: 12.5, fulfilled: 4, onTime: 75, dated: 4, approvalRate: 80, decisions: 5, satisfaction: 4.2, ratings: 3, open: 6, overdue: 1 },
  byItem: [{ item: 'Accès VPN', created: 5, fulfillmentHours: 12.5, onTime: 75, approvalRate: 80, satisfaction: null }],
  scores: [1, 2, 3, 4, 5].map(score => ({ score, count: score === 4 ? 2 : 0 })),
  watch: [{ id: 7, reference: 'REQ-2026-0007', title: 'Poste portable', status: 'En cours', ownerDisplayName: 'Hery', dueDate: '2026-10-20T00:00:00Z', item: 'Poste' }],
}

it('affiche les indicateurs REQ-40 à REQ-43, le détail par modèle et les demandes en retard', () => {
  render(<MemoryRouter><RequestsTab data={data} /></MemoryRouter>)
  expect(screen.getByText('Délai moyen de traitement')).toBeTruthy()
  // Chiffre et unité dans la même tuile (l'unité en plus petit).
  expect([...document.querySelectorAll('.kpi .n')].map(n => n.textContent)).toContain('4,2 / 5')
  expect(screen.getByText('Accès VPN')).toBeTruthy()
  expect(screen.getByRole('link', { name: 'REQ-2026-0007' }).getAttribute('href')).toBe('/requests/7')
  const csv = requestsCsv(data)
  expect(csv.map(s => s.title)).toEqual(['Indicateurs', 'Par modèle du catalogue', 'Avis par note', 'Demandes en retard'])
})
