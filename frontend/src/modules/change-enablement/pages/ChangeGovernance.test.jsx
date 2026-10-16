import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { CabCard } from './ChangeDetail'
import StandardChanges from './StandardChanges'

const decide = vi.fn(() => Promise.resolve({}))
const createModel = vi.fn(() => Promise.resolve({}))
let user = { username: 'chef.b', role: 'Manager' }
vi.mock('../../../api', () => ({
  getUser: () => user,
  api: {
    changes: {
      approvals: () => Promise.resolve([{ id: 1, approver: 'chef.a', approverDisplayName: 'Chef A', approved: true, comment: 'OK', at: '2026-10-16T09:00:00Z' }]),
      decide: (...a) => decide(...a)
    },
    standardChanges: {
      list: () => Promise.resolve([{ id: 2, name: 'Redémarrage serveur', description: '', risk: 'Faible', active: true }]),
      create: (...a) => createModel(...a), update: vi.fn()
    }
  }
}))
vi.mock('../../../core/usePerimeters', () => ({ default: () => [] }))

afterEach(() => { cleanup(); decide.mockClear(); createModel.mockClear(); user = { username: 'chef.b', role: 'Manager' } })

const change = { id: 9, perimeterId: 1, requiresCab: true, status: 'Évalué', createdBy: 'demandeur', updatedAt: 'x' }

describe('CAB (CHG-20)', () => {
  it('montre les décisions et laisse un autre gestionnaire décider ; rejet motivé', async () => {
    const onDecided = vi.fn()
    render(<CabCard change={change} onDecided={onDecided} />)
    expect(await screen.findByText('Chef A')).toBeTruthy()
    expect(screen.getByText('Rejeter').disabled).toBe(true)
    fireEvent.click(screen.getByText('Approuver'))
    await waitFor(() => expect(onDecided).toHaveBeenCalled())
    expect(decide).toHaveBeenCalledWith(9, { approve: true, comment: '' })
  })

  it('le demandeur ne décide pas', async () => {
    user = { username: 'demandeur', role: 'Manager' }
    render(<CabCard change={change} onDecided={() => {}} />)
    expect(await screen.findByText('Chef A')).toBeTruthy()
    expect(screen.queryByText('Approuver')).toBeNull()
  })
})

describe('Modèles standard (CHG-26)', () => {
  it('liste et crée un modèle avec ses plans', async () => {
    render(<StandardChanges />)
    expect(await screen.findByText('Redémarrage serveur')).toBeTruthy()
    fireEvent.click(screen.getByText('+ Nouveau modèle'))
    fireEvent.change(screen.getByLabelText('Nom *'), { target: { value: 'Rotation des journaux' } })
    fireEvent.change(screen.getByLabelText('Plan de mise en œuvre *'), { target: { value: 'Script' } })
    fireEvent.change(screen.getByLabelText('Plan de retour arrière *'), { target: { value: 'Restaurer' } })
    fireEvent.click(screen.getByText('Enregistrer'))
    await waitFor(() => expect(createModel).toHaveBeenCalled())
    expect(createModel.mock.calls[0][0]).toMatchObject({ name: 'Rotation des journaux', risk: 'Faible' })
  })
})
