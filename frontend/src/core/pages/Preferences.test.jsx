import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import Preferences from './Preferences'

const save = vi.fn(dto => Promise.resolve({ ...dto, mailEnabled: false }))
vi.mock('../../api', () => ({
  api: { preferences: { mail: () => Promise.resolve({ mode: 'Aucun', address: null, mailEnabled: false }), saveMail: (...a) => save(...a) } }
}))
afterEach(() => { cleanup(); save.mockClear() })

it('SOC-22 : un envoi exige une adresse ; l’avertissement dit si l’administrateur n’a pas activé les courriels', async () => {
  render(<Preferences />)
  expect(await screen.findByText(/pas encore activé/)).toBeTruthy()
  fireEvent.click(screen.getByLabelText(/Résumé quotidien/))
  const button = screen.getByText('Enregistrer')
  expect(button.disabled).toBe(true)
  fireEvent.change(screen.getByLabelText('Adresse de réception'), { target: { value: 'moi@corp.local' } })
  fireEvent.click(button)
  await waitFor(() => expect(save).toHaveBeenCalledWith({ mode: 'Résumé quotidien', address: 'moi@corp.local' }))
})
