import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Regulatory } from './IncidentDetail'

let role = 'Manager'
const qualify = vi.fn(() => Promise.resolve({ created: ['DRG-2026-0001'], proposeMajor: true }))
vi.mock('../../../api', () => ({
  getUser: () => ({ role }),
  api: {
    incidents: { qualify: (...a) => qualify(...a) },
    regulatoryNotifications: { list: () => Promise.resolve([]) },
  }
}))
afterEach(() => { cleanup(); vi.clearAllMocks(); role = 'Manager' })

const incident = { id: 7, isSecurity: true, qualifiedAt: null }
const page = (i = incident) => render(<MemoryRouter><Regulatory incident={i} reload={() => {}} /></MemoryRouter>)

it('INC-41, INC-44 : un gestionnaire qualifie l’incident de sécurité, majeur proposé si important', async () => {
  page()
  const button = await screen.findByRole('button', { name: 'Qualifier' })
  expect(button.disabled).toBe(true)
  fireEvent.change(screen.getByLabelText('NIS 2'), { target: { value: 'true' } })
  fireEvent.change(screen.getByLabelText('RGPD'), { target: { value: 'false' } })
  fireEvent.change(screen.getByPlaceholderText('Ex. : aucune donnée personnelle concernée'), { target: { value: 'Aucune donnée' } })
  fireEvent.click(button)
  await waitFor(() => expect(qualify).toHaveBeenCalledWith(7, expect.objectContaining({
    nis2Important: true, personalDataBreach: false, personalDataBreachReason: 'Aucune donnée', awareAt: null })))
  expect((await screen.findByRole('status')).textContent).toContain('DRG-2026-0001')
  expect(screen.getByRole('status').textContent).toContain('incident majeur')
})

it('INC-40 : rien pour un incident ordinaire ; un User ne qualifie pas', async () => {
  const { container } = page({ id: 8, isSecurity: false })
  expect(container.textContent).toBe('')
  cleanup()
  role = 'User'
  page()
  expect(await screen.findByText(/À qualifier par un gestionnaire/)).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Qualifier' })).toBeNull()
})
