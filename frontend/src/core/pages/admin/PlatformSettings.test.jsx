import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import PlatformSettings from './PlatformSettings'

const mail = { enabled: true, host: 'smtp.corp.local', port: 587, security: 'STARTTLS', username: 'relais', hasPassword: true,
  fromAddress: 'itsm@corp.local', fromName: 'S-Aloha', updatedAt: '2026-10-23T08:00:00Z', updatedBy: 'Admin' }
const storage = { enabled: false, endpoint: 'http://garage:3900', region: 'garage', bucket: 's-aloha', accessKey: 'GK1', hasSecretKey: false,
  pathStyle: true, maxFileMb: 10, updatedAt: null, updatedBy: null }
const saveMail = vi.fn(dto => Promise.resolve({ ...mail, ...dto, hasPassword: dto.password !== '' }))
const testMail = vi.fn(() => Promise.resolve({ ok: false, message: 'Échec de l’envoi : connexion refusée' }))
const saveStorage = vi.fn(dto => Promise.resolve({ ...storage, ...dto, hasSecretKey: !!dto.secretKey }))
vi.mock('../../../api', () => ({
  api: { admin: {
    mail: () => Promise.resolve(mail), saveMail: (...a) => saveMail(...a), testMail: (...a) => testMail(...a),
    storage: () => Promise.resolve(storage), saveStorage: (...a) => saveStorage(...a), testStorage: () => Promise.resolve({ ok: true, message: 'Objet d’essai déposé' }) } }
}))
afterEach(() => { cleanup(); vi.clearAllMocks() })

it('SMTP : un mot de passe enregistré n’est pas renvoyé tant qu’il n’est pas ressaisi ; essai expliqué', async () => {
  render(<PlatformSettings />)
  expect((await screen.findByLabelText('Mot de passe')).getAttribute('placeholder')).toContain('Enregistré')
  fireEvent.click(screen.getAllByText('Enregistrer')[0])
  await waitFor(() => expect(saveMail).toHaveBeenCalled())
  expect(saveMail.mock.calls[0][0].password).toBeUndefined()
  fireEvent.change(screen.getByLabelText('Destinataire du courriel d’essai'), { target: { value: 'moi@corp.local' } })
  fireEvent.click(screen.getByText('Envoyer un courriel d’essai'))
  expect(await screen.findByText(/connexion refusée/)).toBeTruthy()
  expect(testMail).toHaveBeenCalledWith('moi@corp.local')
})

it('Stockage : la clé secrète saisie part avec les réglages', async () => {
  render(<PlatformSettings />)
  fireEvent.change(await screen.findByLabelText('Clé secrète'), { target: { value: 'secret' } })
  fireEvent.click(screen.getAllByText('Enregistrer')[1])
  await waitFor(() => expect(saveStorage).toHaveBeenCalled())
  expect(saveStorage.mock.calls[0][0]).toMatchObject({ secretKey: 'secret', bucket: 's-aloha', maxFileMb: 10 })
  fireEvent.click(screen.getByText('Tester le dépôt'))
  expect(await screen.findByText('Objet d’essai déposé')).toBeTruthy()
})
