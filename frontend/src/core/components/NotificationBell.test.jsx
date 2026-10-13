import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import NotificationBell from './NotificationBell'

const read = vi.fn(() => Promise.resolve())
vi.mock('../../api', () => ({
  api: { notifications: {
    list: () => Promise.resolve({ unread: 1, items: [
      { id: 5, targetType: 'incident', targetId: 12, reference: 'INC-2026-0012', title: 'Imprimante', event: 'Confié à vous',
        actorDisplayName: 'Marc', createdAt: '2026-10-13T08:00:00Z', read: false }] }),
    read: (...a) => read(...a), readAll: vi.fn(),
  } },
}))

afterEach(() => { cleanup(); read.mockClear() })

describe('Cloche des notifications (SOC-21)', () => {
  it('affiche le compteur, et une notification ouverte est lue puis mène à la fiche', async () => {
    render(<MemoryRouter initialEntries={['/']}><Routes>
      <Route path="/" element={<NotificationBell />} />
      <Route path="/incidents/12" element={<p>Fiche INC</p>} />
    </Routes></MemoryRouter>)
    const bell = await screen.findByRole('button', { name: 'Notifications : 1 non lue(s)' })
    fireEvent.click(bell)
    fireEvent.click(screen.getByText('Confié à vous'))
    await waitFor(() => expect(read).toHaveBeenCalledWith(5))
    expect(await screen.findByText('Fiche INC')).toBeTruthy()
  })
})
