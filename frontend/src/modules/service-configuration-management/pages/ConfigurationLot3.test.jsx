import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Attributes } from './ConfigurationItemDetail'
import { CiImport } from './ConfigurationItems'

const update = vi.fn(() => Promise.resolve({}))
const importCsv = vi.fn((csv, dryRun) => dryRun
  ? Promise.reject(Object.assign(new Error('1 ligne(s) refusée(s) : rien n’a été importé.'), { body: { errors: [{ line: 3, name: 'srv-b', message: 'Type de CI invalide' }] } }))
  : Promise.resolve({ message: '2 CI importés.', created: 2, errors: [] }))
vi.mock('../../../api', () => ({
  getUser: () => ({ role: 'Admin' }),
  api: { configurationItems: {
    attributeTypes: () => Promise.resolve({ Serveur: [{ key: 'cpu', label: 'Processeurs (cœurs)', kind: 'nombre' }, { key: 'memoryGb', label: 'Mémoire (Go)', kind: 'nombre' }], Autre: [] }),
    update: (...a) => update(...a), importCsv: (...a) => importCsv(...a) } }
}))
afterEach(() => { cleanup(); vi.clearAllMocks() })

it('CFG-16 : les attributs du type s’affichent et s’enregistrent avec la fiche', async () => {
  const ci = { id: 9, title: 'srv-1', ciType: 'Serveur', attributes: { cpu: '8' } }
  render(<Attributes ci={ci} onSaved={() => {}} />)
  expect(await screen.findByText('8')).toBeTruthy()
  fireEvent.click(screen.getByText('Modifier les attributs'))
  fireEvent.change(screen.getByLabelText('Mémoire (Go)'), { target: { value: '32' } })
  fireEvent.click(screen.getByText('Enregistrer'))
  await waitFor(() => expect(update).toHaveBeenCalledWith(9, { ...ci, attributes: { cpu: '8', memoryGb: '32' } }))
})

it('CFG-16 : rien pour un type sans attributs', async () => {
  const { container } = render(<Attributes ci={{ id: 1, ciType: 'Autre', attributes: {} }} onSaved={() => {}} />)
  await waitFor(() => expect(container.textContent).toBe(''))
})

it('CFG-30 : le contrôle liste les lignes refusées, l’import confirme', async () => {
  render(<CiImport />)
  fireEvent.change(screen.getByLabelText('Contenu CSV'), { target: { value: 'nom;type\nsrv-a;Serveur' } })
  fireEvent.click(screen.getByText('Contrôler'))
  expect(await screen.findByText(/Ligne 3 \(srv-b\) : Type de CI invalide/)).toBeTruthy()
  fireEvent.click(screen.getByText('Importer'))
  expect(await screen.findByText('2 CI importés.')).toBeTruthy()
})
