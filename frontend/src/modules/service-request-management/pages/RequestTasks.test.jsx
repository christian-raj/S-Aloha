import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { RequestTasks, Satisfaction } from './RequestDetail'

const update = vi.fn(() => Promise.resolve({}))
const rate = vi.fn(() => Promise.resolve({}))
vi.mock('../../../api', () => ({
  getUser: () => ({ username: 'demandeur', role: 'User' }),
  api: { requests: {
    tasks: () => Promise.resolve([
      { id: 1, title: 'Préparer le poste', status: 'Terminée', ownerDisplayName: 'Tech', fromCatalog: true },
      { id: 2, title: 'Livrer et former', status: 'À faire', ownerDisplayName: 'GRP-POSTES', fromCatalog: true }]),
    updateTask: (...a) => update(...a), addTask: () => Promise.resolve({}), rate: (...a) => rate(...a),
  } }
}))
afterEach(() => { cleanup(); vi.clearAllMocks() })

it('REQ-24, REQ-25 : tâches ouvertes signalées, statut modifiable', async () => {
  render(<MemoryRouter><RequestTasks request={{ id: 5, status: 'En cours' }} /></MemoryRouter>)
  expect(await screen.findByText(/1 tâche\(s\) encore ouverte\(s\)/)).toBeTruthy()
  fireEvent.change(screen.getByLabelText('Statut de « Livrer et former »'), { target: { value: 'Terminée' } })
  await waitFor(() => expect(update).toHaveBeenCalledWith(5, 2, expect.objectContaining({ status: 'Terminée' })))
})

it('REQ-32 : le demandeur note la demande close ; rien pour une demande ouverte', async () => {
  const { container } = render(<MemoryRouter><Satisfaction request={{ id: 5, status: 'Satisfaite', createdBy: 'demandeur' }} /></MemoryRouter>)
  expect(container.textContent).toBe('')
  cleanup()
  render(<MemoryRouter><Satisfaction request={{ id: 5, status: 'Close', createdBy: 'demandeur' }} /></MemoryRouter>)
  fireEvent.click(screen.getByLabelText('Note 4 sur 5'))
  fireEvent.click(screen.getByRole('button', { name: 'Envoyer' }))
  await waitFor(() => expect(rate).toHaveBeenCalledWith(5, { score: 4, comment: '' }))
})
