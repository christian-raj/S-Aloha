import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { FollowUp } from './ImprovementDetail'
import { Rubrics } from '../../regulatory-notifications/pages/DeclarationDetail'

const followUp = vi.fn(() => Promise.resolve({ improvement: {}, created: 'AMI-2026-0042' }))
vi.mock('../../../api', () => ({
  getUser: () => ({ username: 'porteur', role: 'User' }),
  api: { improvements: { followUp: (...a) => followUp(...a) } }
}))
afterEach(() => { cleanup(); followUp.mockClear() })

const improvement = { id: 3, status: 'Réalisée', followUpDate: '2026-01-01T00:00:00Z', followUpDoneAt: null, ownerType: 'User', ownerId: 'porteur' }

it('CSI-15 : le porteur conclut le contrôle ; « ne tient pas » exige un constat et ouvre une amélioration', async () => {
  const onDone = vi.fn()
  render(<MemoryRouter><FollowUp improvement={improvement} onDone={onDone} /></MemoryRouter>)
  const fails = screen.getByText('Ne tient pas : ouvrir une amélioration')
  expect(fails.disabled).toBe(true)
  fireEvent.change(screen.getByLabelText('Constat'), { target: { value: 'Les doublons reviennent' } })
  fireEvent.click(fails)
  await waitFor(() => expect(followUp).toHaveBeenCalledWith(3, { holds: false, note: 'Les doublons reviennent' }))
  expect(await screen.findByText('AMI-2026-0042')).toBeTruthy()
})

it('DRG-08 : les rubriques attendues encore vides sont signalées', () => {
  render(<Rubrics r={{ regime: 'NIS 2', authority: 'ANSSI', missingRubrics: ['Gravité', 'Indicateurs de compromission'] }} />)
  expect(screen.getByRole('status').textContent).toContain('Gravité, Indicateurs de compromission')
})
