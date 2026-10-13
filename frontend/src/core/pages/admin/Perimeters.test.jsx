import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Perimeters from './Perimeters'
import PerimeterDetail from './PerimeterDetail'

const restore = vi.fn(() => Promise.resolve({}))
const assign = vi.fn(() => Promise.resolve({}))
let detail
vi.mock('../../../api', () => ({
  api: {
    perimeters: {
      list: () => Promise.resolve([
        { id: 1, name: 'Organisation', description: 'Défaut', assignments: 0, deletedAt: null },
        { id: 4, name: 'Ancien site', description: '', assignments: 0, deletedAt: '2026-10-01T00:00:00Z' }]),
      get: () => Promise.resolve(detail),
      restore: (...a) => restore(...a), assign: (...a) => assign(...a),
      create: vi.fn(), update: vi.fn(), remove: vi.fn(), purge: vi.fn(), unassign: vi.fn(),
    },
    directory: { search: () => Promise.resolve([{ type: 'Group', id: 'GRP-USINE', displayName: 'Équipe usine' }]) },
  },
}))

afterEach(() => { cleanup(); restore.mockClear(); assign.mockClear() })

const at = (path, el) => render(<MemoryRouter initialEntries={[path]}><Routes><Route path="/admin/perimeters/:id" element={el} />
  <Route path="/admin/perimeters" element={el} /></Routes></MemoryRouter>)

describe('Administration › Périmètres', () => {
  it('liste les périmètres, un supprimé se restaure (PER-01)', async () => {
    at('/admin/perimeters', <Perimeters />)
    expect(await screen.findByText('Ancien site')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Restaurer' }))
    await waitFor(() => expect(restore).toHaveBeenCalledWith(4))
    expect(screen.getByRole('button', { name: 'Créer' }).disabled).toBe(true)
  })

  it('fiche : suppression définitive bloquée tant que des enregistrements y sont rattachés (PER-01, PER-12)', async () => {
    detail = { id: 5, name: 'Usine', description: '', createdAt: '2026-10-12T00:00:00Z', deletedAt: '2026-10-13T00:00:00Z',
      deletedBy: 'demo.admin', isDefault: false, counts: [{ practice: 'Incidents', count: 2 }], assignments: [] }
    at('/admin/perimeters/5', <PerimeterDetail />)
    expect((await screen.findByRole('button', { name: 'Supprimer définitivement' })).disabled).toBe(true)
    expect(screen.getByText('Enregistrements rattachés (2)')).toBeTruthy()
  })

  it('affecte un groupe AD avec le rôle choisi (PER-02)', async () => {
    detail = { id: 6, name: 'Siège', description: '', createdAt: '2026-10-12T00:00:00Z', deletedAt: null,
      isDefault: false, counts: [], assignments: [] }
    at('/admin/perimeters/6', <PerimeterDetail />)
    fireEvent.change(await screen.findByLabelText("Rôle de l'affectation"), { target: { value: 'Manager' } })
    fireEvent.change(screen.getByPlaceholderText(/Affecter un utilisateur/), { target: { value: 'usine' } })
    fireEvent.click(await screen.findByText(/Équipe usine/))
    await waitFor(() => expect(assign).toHaveBeenCalledWith(6,
      { principalType: 'Group', principalId: 'GRP-USINE', principalDisplayName: 'Équipe usine', role: 'Manager' }))
  })
})
