import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ProblemDetail from './ProblemDetail'

// Un problème sans analyse ni action : c'est le cas où démarrer la PREMIÈRE
// analyse faisait planter l'onglet (B1, revue du 2026-10-06).
vi.mock('../../../api', () => ({
  getUser: () => ({ username: 'hery.rakoto', displayName: 'Hery Rakoto', role: 'Admin' }),
  api: {
    problems: {
      get: () => Promise.resolve({
        id: 1, reference: 'PRB-2026-0001', title: 'Lenteurs messagerie', description: '',
        status: 'Nouveau', impact: 'Moyen', urgency: 'Moyenne', priority: 'P3', category: '',
        affectedService: '', createdByDisplayName: 'Hery Rakoto', createdAt: '2026-10-06T08:00:00Z',
        analyses: [], actions: []
      })
    },
    analyses: { create: vi.fn(), update: vi.fn() }
  }
}))

const renderDetail = () => render(
  <MemoryRouter initialEntries={['/problems/1']}>
    <Routes><Route path="/problems/:id" element={<ProblemDetail />} /></Routes>
  </MemoryRouter>
)

afterEach(cleanup)

describe('Analyse de cause racine — démarrer une analyse (B1)', () => {
  it.each([
    ['+ 5 Pourquoi', 'Énoncé du problème'],
    ['+ Ishikawa (6M)', 'Méthode'],
    ['+ Arbre des défaillances (FTA)', 'Cause racine identifiée'],
  ])('« %s » ouvre l\'éditeur sans planter', async (button, expected) => {
    renderDetail()
    fireEvent.click(await screen.findByText('Analyse de cause racine'))
    fireEvent.click(screen.getByText(button))
    expect(await screen.findByText(expected)).toBeTruthy()
    expect(screen.getByText("Enregistrer l'analyse")).toBeTruthy()
  })
})
