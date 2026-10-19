import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Duplicates from './Duplicates'

const merge = vi.fn(() => Promise.resolve({ linksMoved: 1, commentsMoved: 2 }))
const dismiss = vi.fn(() => Promise.resolve(null))
vi.mock('../../../api', () => ({
  api: { admin: {
    duplicates: () => Promise.resolve([{ type: 'incident',
      first: { id: 1, reference: 'INC-2026-0001', title: 'Messagerie lente', status: 'Nouveau', createdAt: '2026-10-18T08:00:00Z', links: 0, comments: 0 },
      second: { id: 2, reference: 'INC-2026-0002', title: 'messagerie LENTE', status: 'Nouveau', createdAt: '2026-10-18T09:00:00Z', links: 1, comments: 2 } }]),
    mergeDuplicate: (...a) => merge(...a), dismissDuplicate: (...a) => dismiss(...a) } }
}))
afterEach(() => { cleanup(); merge.mockClear() })

it('ADM-41, ADM-43 : la fusion se confirme et annonce ce qui est reporté', async () => {
  render(<MemoryRouter><Duplicates /></MemoryRouter>)
  fireEvent.click(await screen.findByRole('button', { name: 'Conserver INC-2026-0001' }))
  const dialog = await screen.findByRole('alertdialog')
  expect(dialog.textContent).toContain('Fusionner INC-2026-0002 dans INC-2026-0001')
  expect(dialog.textContent).toContain('2 commentaire(s)')
  fireEvent.click(screen.getByRole('button', { name: 'Fusionner' }))
  await waitFor(() => expect(merge).toHaveBeenCalledWith({ type: 'incident', keepId: 1, removeId: 2 }))
})

it('ADM-42 : « Pas un doublon » écarte la paire', async () => {
  render(<MemoryRouter><Duplicates /></MemoryRouter>)
  fireEvent.click(await screen.findByRole('button', { name: 'Pas un doublon' }))
  await waitFor(() => expect(dismiss).toHaveBeenCalledWith({ type: 'incident', firstId: 1, secondId: 2 }))
})
