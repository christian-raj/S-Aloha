import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Family } from './IncidentDetail'

let family = { parent: null, children: [] }
const setParent = vi.fn(() => Promise.resolve({}))
vi.mock('../../../api', () => ({
  getUser: () => ({ role: 'User' }),
  api: { incidents: { family: () => Promise.resolve(family), setParent: (...a) => setParent(...a) } }
}))
afterEach(() => { cleanup(); vi.clearAllMocks(); family = { parent: null, children: [] } })

const page = (i = { id: 5, status: 'Nouveau' }, reload = () => {}) =>
  render(<MemoryRouter><Family incident={i} reload={reload} /></MemoryRouter>)

it('INC-16 : rattache un incident à son parent par la référence', async () => {
  const reload = vi.fn()
  page(undefined, reload)
  fireEvent.change(await screen.findByLabelText('Référence de l’incident parent'), { target: { value: ' INC-2026-0042 ' } })
  fireEvent.click(screen.getByText('Rattacher à cet incident parent'))
  await waitFor(() => expect(reload).toHaveBeenCalled())
  expect(setParent).toHaveBeenCalledWith(5, 'INC-2026-0042')
})

it('INC-16 : un parent liste ses enfants ; un enfant se détache', async () => {
  family = { parent: null, children: [{ id: 6, reference: 'INC-2026-0006', title: 'Messagerie', status: 'En cours', priority: 'P3' }] }
  page()
  expect(await screen.findByText('INC-2026-0006')).toBeTruthy()
  expect(screen.queryByLabelText('Référence de l’incident parent')).toBeNull()
  cleanup()
  family = { parent: { id: 4, reference: 'INC-2026-0004', title: 'Coupure réseau', status: 'En cours', priority: 'P2' }, children: [] }
  page()
  fireEvent.click(await screen.findByText('Détacher'))
  await waitFor(() => expect(setParent).toHaveBeenCalledWith(5, ''))
})
