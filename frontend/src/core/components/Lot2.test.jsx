import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import TypingSuggestions from './TypingSuggestions'
import SearchHits from './SearchHits'
import { Degradation } from '../../modules/service-level-management/pages/ServiceDetail'

const query = vi.fn(() => Promise.resolve([{ type: 'article', id: 3, reference: 'KB-2026-0003', title: 'VPN : réinitialiser le client', status: 'Publié', snippet: 's', lexical: true }]))
const link = vi.fn(() => Promise.resolve({}))
vi.mock('../../api', () => ({
  api: {
    search: { query: (...a) => query(...a) },
    links: { create: (...a) => link(...a) },
    services: { degradation: () => Promise.resolve({ degraded: true, cis: [{ id: 9, reference: 'CI-2026-0009', title: 'srv-paie' }] }) },
  }
}))
afterEach(() => { cleanup(); vi.clearAllMocks() })

it('RAG-11 : cas similaires dès la saisie du titre, après une pause de frappe', async () => {
  const { rerender } = render(<MemoryRouter><TypingSuggestions text="VP" delay={10} /></MemoryRouter>)
  rerender(<MemoryRouter><TypingSuggestions text="VPN bloqué" delay={10} /></MemoryRouter>)
  expect(await screen.findByText(/Déjà vu/)).toBeTruthy()
  expect(query).toHaveBeenCalledWith('VPN bloqué')
  expect(query).toHaveBeenCalledTimes(1)   // « VP » : trop court pour chercher
})

it('RAG-09 : relier en un clic l’article retenu à l’incident', async () => {
  render(<MemoryRouter><SearchHits linkFrom={{ type: 'incident', id: 7 }}
    hits={[{ type: 'article', id: 3, reference: 'KB-2026-0003', title: 'VPN', status: 'Publié', snippet: '', lexical: true },
      { type: 'incident', id: 4, reference: 'INC-2026-0004', title: 'VPN', status: 'Résolu', snippet: '', semantic: true }]} /></MemoryRouter>)
  const buttons = screen.getAllByRole('button', { name: 'Relier' })
  expect(buttons).toHaveLength(1)   // un incident résolu se consulte, il ne se relie pas d'ici
  fireEvent.click(buttons[0])
  await waitFor(() => expect(link).toHaveBeenCalledWith({ fromType: 'incident', fromId: 7, toReference: 'KB-2026-0003' }))
  expect(await screen.findByText('Relié')).toBeTruthy()
})

it('CFG-18 : un service dégradé l’affiche, avec le CI hors service', async () => {
  render(<MemoryRouter><Degradation service={{ id: 2, status: 'En service' }} /></MemoryRouter>)
  expect((await screen.findByRole('alert')).textContent).toContain('CI-2026-0009')
})
