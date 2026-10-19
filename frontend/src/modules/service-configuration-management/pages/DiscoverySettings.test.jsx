import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import DiscoverySettings from './DiscoverySettings'

let role = 'Admin'
const importOui = vi.fn(() => Promise.resolve({ imported: 2, skipped: 1 }))
vi.mock('../../../api', () => ({
  getUser: () => ({ role }),
  api: { discovery: { macVendors: {
    get: () => Promise.resolve({ count: 0, lastImport: null }),
    import: (...a) => importOui(...a) } } }
}))
afterEach(() => { cleanup(); importOui.mockClear(); role = 'Admin' })

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
