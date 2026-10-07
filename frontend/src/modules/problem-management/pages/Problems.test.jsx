import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Problems from './Problems'

const list = vi.fn(() => Promise.resolve([]))
vi.mock('../../../api', () => ({ api: { problems: { list: (...a) => list(...a) } } }))

afterEach(() => { cleanup(); list.mockClear() })

// L'entrée « Erreurs connues » de la navigation est ce registre filtré par
// l'URL : la page doit lire le statut dans la requête, pas dans son état local.
it('lit le filtre de statut dans l\'URL', async () => {
  render(
    <MemoryRouter initialEntries={['/problems?' + new URLSearchParams({ status: 'Erreur connue' })]}>
      <Routes><Route path="/problems" element={<Problems />} /></Routes>
    </MemoryRouter>
  )
  await waitFor(() => expect(list).toHaveBeenCalledWith({ status: 'Erreur connue' }))
  expect(screen.getByRole('heading', { name: 'Erreurs connues' })).toBeTruthy()
  expect(screen.getByRole('combobox').value).toBe('Erreur connue')
})
