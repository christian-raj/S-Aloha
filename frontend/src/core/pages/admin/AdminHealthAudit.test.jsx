import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Health from './Health'
import AuditLog from './AuditLog'

const audit = vi.fn(() => Promise.resolve({ total: 1, page: 1, pageSize: 100, items: [
  { id: 1, at: '2026-10-19T10:00:00Z', authorDisplayName: 'Hery', entityType: 'incident', entityId: 4, reference: 'INC-2026-0004',
    action: 'Modification', field: 'status', oldValue: 'Nouveau', newValue: 'En cours', reason: null }] }))
const auditExport = vi.fn(() => Promise.resolve())
vi.mock('../../../api', () => ({
  api: { admin: {
    health: () => Promise.resolve({ state: 'Dégradé', version: '0.3.0', commit: 'abc123', environment: 'Production',
      startedAt: '2026-10-19T08:00:00Z', checkedAt: '2026-10-19T10:00:00Z', components: [
        { name: 'Base de données', state: 'OK', message: 'Connectée, schéma à jour.', durationMs: 12 },
        { name: 'Annuaire', state: 'Dégradé', message: 'Lent', durationMs: 900 }] }),
    audit: (...a) => audit(...a), auditExport: (...a) => auditExport(...a) } }
}))
afterEach(() => { cleanup(); audit.mockClear() })

it('Santé : état global, version et chaque composant', async () => {
  render(<Health />)
  expect(await screen.findByText('Base de données')).toBeTruthy()
  expect(screen.getByText(/Version 0.3.0/).textContent).toContain('abc123')
  expect(screen.getAllByText('Dégradé').length).toBe(2)
})

it('Journal d\'audit : lignes liées à la fiche, filtres envoyés à l\'API, export', async () => {
  render(<MemoryRouter><AuditLog /></MemoryRouter>)
  expect((await screen.findByText('INC-2026-0004')).closest('a').getAttribute('href')).toBe('/incidents/4')
  fireEvent.change(screen.getByLabelText('Auteur'), { target: { value: 'hery' } })
  fireEvent.click(screen.getByRole('button', { name: 'Filtrer' }))
  await waitFor(() => expect(audit).toHaveBeenLastCalledWith(expect.objectContaining({ author: 'hery', page: 1 })))
  fireEvent.click(screen.getByRole('button', { name: 'Exporter (CSV / Excel)' }))
  expect(auditExport).toHaveBeenCalledWith(expect.objectContaining({ author: 'hery' }))
})
