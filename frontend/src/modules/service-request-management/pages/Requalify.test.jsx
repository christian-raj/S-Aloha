import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Requalify } from './RequestDetail'

let user = { username: 'demandeur', role: 'User' }
const requalify = vi.fn(() => Promise.resolve({ target: { type: 'change', id: 12, reference: 'CHG-2026-0012' } }))
vi.mock('../../../api', () => ({ getUser: () => user, api: { requests: { requalify: (...a) => requalify(...a) } } }))
afterEach(() => { cleanup(); vi.clearAllMocks(); user = { username: 'demandeur', role: 'User' } })

const page = (r) => render(<MemoryRouter><Requalify request={{ id: 4, createdBy: 'demandeur', status: 'Soumise', ...r }} /></MemoryRouter>)

it('REQ-12 : le demandeur requalifie sa demande en changement, motif exigé', async () => {
  page()
  const button = screen.getByRole('button', { name: 'Requalifier' })
  expect(button.disabled).toBe(true)
  fireEvent.change(screen.getByLabelText('Motif de la requalification'), { target: { value: 'Hors catalogue' } })
  fireEvent.click(button)
  await waitFor(() => expect(requalify).toHaveBeenCalledWith(4, { target: 'change', reason: 'Hors catalogue' }))
})

it('REQ-12 : ni un tiers, ni une demande en cours', () => {
  user = { username: 'autre', role: 'User' }
  const { container } = page()
  expect(container.textContent).toBe('')
  cleanup()
  user = { username: 'demandeur', role: 'User' }
  expect(page({ status: 'En cours' }).container.textContent).toBe('')
})
