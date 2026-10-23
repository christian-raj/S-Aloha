import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import StandardChanges from './StandardChanges'

const review = vi.fn(() => Promise.resolve({}))
const models = [{ id: 3, name: 'Correctifs mensuels', description: '', risk: 'Faible', active: true,
  suspendedAt: '2026-10-23T08:00:00Z', suspendedByChangeId: 12, suspendedByChange: 'CHG-2026-0012' }]
vi.mock('../../../api', () => ({
  getUser: () => ({ role: 'Manager' }),
  api: { standardChanges: { list: () => Promise.resolve(models), create: vi.fn(), update: vi.fn(), review: (...a) => review(...a) } }
}))
afterEach(() => { cleanup(); review.mockClear() })

it('CHG-27 : un modèle suspendu se revoit, conclusion obligatoire, puis remise en service', async () => {
  render(<MemoryRouter><StandardChanges /></MemoryRouter>)
  expect(await screen.findByText('Suspendu (CHG-2026-0012)')).toBeTruthy()
  fireEvent.click(screen.getByText('Revoir'))
  const keep = screen.getByText('Remettre en service')
  expect(keep.disabled).toBe(true)
  fireEvent.change(screen.getByLabelText('Conclusion de la revue *'), { target: { value: 'Plan complété' } })
  fireEvent.click(keep)
  await waitFor(() => expect(review).toHaveBeenCalledWith(3, { note: 'Plan complété', keep: true }))
})
