import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import RecordDetail from './RecordDetail'

vi.mock('../../api', () => ({
  getUser: () => ({ username: 'u', displayName: 'U', role: 'Admin' }),
  api: { links: { list: () => Promise.resolve([]) } }
}))

const config = {
  title: 'Changements', basePath: '/changes', linkType: 'change', fields: [],
  statuses: [{ value: 'Nouveau' }],
  api: { get: () => Promise.resolve({ id: 7, reference: 'CHG-2026-0007', title: 'Test', status: 'Nouveau',
    createdByDisplayName: 'U', createdAt: '2026-10-07T08:00:00Z' }) }
}

afterEach(cleanup)

// docs/reference/frontend.md § Navigation cible — onglets communs des fiches.
it('ordonne les onglets : Informations, propre à la pratique, Liens, Historique', async () => {
  render(
    <MemoryRouter initialEntries={['/changes/7']}>
      <Routes><Route path="/changes/:id" element={
        <RecordDetail config={config} tab={{ label: 'Conflits', render: () => <p>conflits</p> }} />} /></Routes>
    </MemoryRouter>
  )
  const tabs = (await screen.findAllByRole('tab')).map((t) => t.textContent)
  expect(tabs).toEqual(['Informations', 'Conflits', 'Liens', 'Historique'])
})
