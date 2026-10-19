import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Categories from './Categories'

const list = vi.fn(practice => Promise.resolve(practice === 'incident'
  ? [{ id: 1, practice, name: 'Réseau', active: true, usage: 3 }, { id: 2, practice, name: 'Imprimantes', active: false, usage: 0 }]
  : []))
const create = vi.fn(() => Promise.resolve({}))
const update = vi.fn(() => Promise.resolve({}))
vi.mock('../../../api', () => ({
  api: { categories: { list: (...a) => list(...a), create: (...a) => create(...a), update: (...a) => update(...a), remove: () => Promise.resolve(null) } }
}))
afterEach(() => { cleanup(); vi.clearAllMocks() })

it('ADM-50 : ajouter une catégorie à la pratique affichée', async () => {
  render(<MemoryRouter><Categories /></MemoryRouter>)
  expect(await screen.findByText('1 catégorie(s) active(s) : la catégorie se choisit dans la liste.')).toBeTruthy()
  fireEvent.change(screen.getByLabelText('Nouvelle catégorie'), { target: { value: 'Serveur' } })
  fireEvent.click(screen.getByRole('button', { name: 'Ajouter' }))
  await waitFor(() => expect(create).toHaveBeenCalledWith({ practice: 'incident', name: 'Serveur' }))
})

it('ADM-53 : une catégorie utilisée se désactive mais ne se supprime pas', async () => {
  render(<MemoryRouter><Categories /></MemoryRouter>)
  await screen.findByLabelText('Nom de la catégorie Réseau')
  expect(screen.getAllByRole('button', { name: 'Supprimer' })).toHaveLength(1)   // seule « Imprimantes », inutilisée
  fireEvent.click(screen.getByRole('button', { name: 'Désactiver' }))
  await waitFor(() => expect(update).toHaveBeenCalledWith(1, { practice: 'incident', name: 'Réseau', active: false }))
})

it('ADM-51 : sans catégorie active, la saisie est libre', async () => {
  render(<MemoryRouter initialEntries={['/settings/categories?practice=problem']}><Categories /></MemoryRouter>)
  expect(await screen.findByText('Aucune catégorie active : la catégorie se saisit librement.')).toBeTruthy()
  expect(list).toHaveBeenCalledWith('problem')
})
