import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import DiscoverySettings from './DiscoverySettings'

let role = 'Admin'
const importOui = vi.fn(() => Promise.resolve({ imported: 2, skipped: 1 }))
const createCredential = vi.fn(() => Promise.resolve({}))
vi.mock('../../../api', () => ({
  getUser: () => ({ role }),
  api: {
    perimeters: { mine: () => Promise.resolve([{ id: 1, name: 'Siège' }]), list: () => Promise.resolve([{ id: 1, name: 'Siège' }]) },
    discovery: {
      macVendors: { get: () => Promise.resolve({ count: 0, lastImport: null }), import: (...a) => importOui(...a) },
      credentials: { list: () => Promise.resolve({ vaultConfigured: true, items: [] }), create: (...a) => createCredential(...a) }
    }
  }
}))
afterEach(() => { cleanup(); vi.clearAllMocks(); role = 'Admin' })

const page = () => render(<MemoryRouter initialEntries={['/settings/discovery?tab=oui']}><DiscoverySettings /></MemoryRouter>)

it('CFG-50 : l’Admin importe le registre OUI de l’IEEE', async () => {
  page()
  expect(await screen.findByText(/Aucun préfixe importé/)).toBeTruthy()
  const file = new File(['Registry,Assignment,Organization Name\nMA-L,001A2B,Fabricant\n'], 'oui.csv', { type: 'text/csv' })
  fireEvent.change(screen.getByLabelText('Fichier oui.csv'), { target: { files: [file] } })
  await waitFor(() => expect(importOui).toHaveBeenCalledWith(expect.stringContaining('MA-L,001A2B,Fabricant')))
  expect(await screen.findByRole('status')).toHaveProperty('textContent', expect.stringContaining('2 préfixe(s), 1 ligne(s) écartée(s)'))
})

it('CFG-50 : le gestionnaire voit l’état de la base sans pouvoir importer', async () => {
  role = 'Manager'
  page()
  expect(await screen.findByText('L’import est réservé aux administrateurs.')).toBeTruthy()
  expect(screen.queryByLabelText('Fichier oui.csv')).toBeNull()
})

it('CFG-51 : un utilisateur SNMP v3 porte ses protocoles et ses deux phrases', async () => {
  render(<MemoryRouter initialEntries={['/settings/discovery?tab=credentials']}><DiscoverySettings /></MemoryRouter>)
  fireEvent.click(await screen.findByRole('button', { name: '+ Nouvel identifiant' }))
  fireEvent.change(screen.getByPlaceholderText('Ex. : Communauté SNMP du siège'), { target: { value: 'Utilisateur v3' } })
  fireEvent.change(screen.getByDisplayValue('Communauté SNMP v2c'), { target: { value: 'SnmpV3' } })
  expect(screen.queryByRole('option', { name: /MD5/ })).toBeNull()
  fireEvent.change(screen.getByPlaceholderText('svc-snmp'), { target: { value: 'svc-snmp' } })
  const [auth, priv] = document.querySelectorAll('input[type=password]')
  fireEvent.change(auth, { target: { value: 'phrase-auth' } })
  fireEvent.change(priv, { target: { value: 'phrase-priv' } })
  fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }))
  await waitFor(() => expect(createCredential).toHaveBeenCalledWith(expect.objectContaining({
    credentialType: 'SnmpV3', username: 'svc-snmp', authProtocol: 'SHA256', privProtocol: 'AES', secret: 'phrase-auth', privacySecret: 'phrase-priv' })))
})
