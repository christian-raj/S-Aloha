import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import RequestCatalog from './RequestCatalog'

const create = vi.fn(() => Promise.resolve({}))
let role = 'Manager'
vi.mock('../../../api', () => ({
  getUser: () => ({ role }),
  api: {
    services: { list: () => Promise.resolve([{ id: 1, title: 'Postes de travail', status: 'En service' }]) },
    requestCatalog: {
      list: () => Promise.resolve([
        { id: 3, name: 'Clavier', description: '', serviceId: 1, requiresApproval: false, targetHours: 24, fulfillmentGroup: 'GRP-SUPPORT', active: true },
        { id: 4, name: 'Fax', description: '', serviceId: null, requiresApproval: true, targetHours: null, fulfillmentGroup: null, active: false }]),
      create: (...a) => create(...a), update: vi.fn()
    }
  }
}))

afterEach(() => { cleanup(); create.mockClear(); role = 'Manager' })

describe('Catalogue de demandes (REQ-20)', () => {
  it('liste les modèles et en crée un', async () => {
    render(<RequestCatalog />)
    expect(await screen.findByText('Clavier')).toBeTruthy()
    expect(screen.getByText('Pré-approuvé')).toBeTruthy()
    expect(screen.getByText('Désactivé')).toBeTruthy()
    fireEvent.click(screen.getByText('+ Nouveau modèle'))
    fireEvent.change(screen.getByLabelText('Objet demandé *'), { target: { value: 'Accès VPN' } })
    fireEvent.change(screen.getByLabelText('Délai cible (heures de service)'), { target: { value: '8' } })
    fireEvent.click(screen.getByText('Enregistrer'))
    await waitFor(() => expect(create).toHaveBeenCalled())
    expect(create.mock.calls[0][0]).toMatchObject({ name: 'Accès VPN', targetHours: 8, serviceId: null, requiresApproval: true })
  })

  it('un utilisateur consulte sans modifier', async () => {
    role = 'User'
    render(<RequestCatalog />)
    expect(await screen.findByText('Clavier')).toBeTruthy()
    expect(screen.queryByText('+ Nouveau modèle')).toBeNull()
  })
})
