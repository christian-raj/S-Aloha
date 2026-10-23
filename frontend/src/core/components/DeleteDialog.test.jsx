import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import RecordDetail from './RecordDetail'

vi.mock('../../api', () => ({
  getUser: () => ({ username: 'u', displayName: 'U', role: 'Admin' }),
  api: { links: { list: () => Promise.resolve([]) } }
}))

const remove = vi.fn(() => Promise.resolve(null))
const config = {
  title: 'Changements', basePath: '/changes', linkType: 'change', fields: [],
  statuses: [{ value: 'Nouveau' }],
  api: {
    get: () => Promise.resolve({ id: 7, reference: 'CHG-2026-0007', title: 'Test', status: 'Nouveau',
      createdByDisplayName: 'U', createdAt: '2026-10-07T08:00:00Z' }),
    transitions: () => Promise.resolve({ Nouveau: [] }),
    deletion: () => Promise.resolve({ id: 7, reference: 'CHG-2026-0007', title: 'Test',
      impacts: [{ label: 'liens vers d\'autres enregistrements', count: 2 }, { label: 'décisions du CAB', count: 1 }] }),
    remove: (...a) => remove(...a),
  }
}

afterEach(() => { cleanup(); remove.mockClear() })

const open = () => render(
  <MemoryRouter initialEntries={['/changes/7']}>
    <Routes>
      <Route path="/changes/:id" element={<RecordDetail config={config} />} />
      <Route path="/changes" element={<p>Registre</p>} />
    </Routes>
  </MemoryRouter>)

// SOC-14 : la confirmation rappelle la référence et compte ce qui part avec la suppression.
it('la suppression demande confirmation, liste ce qui part avec et revient au registre', async () => {
  open()
  fireEvent.click(await screen.findByRole('button', { name: 'Supprimer' }))
  const dialog = await screen.findByRole('alertdialog')
  expect(dialog.textContent).toContain('CHG-2026-0007')
  expect(dialog.textContent).toContain('2 liens vers d\'autres enregistrements')
  expect(dialog.textContent).toContain('1 décisions du CAB')
  // Le focus est posé par un effet, après le rendu : on l'attend.
  await waitFor(() => expect(document.activeElement.textContent).toBe('Annuler'))
  fireEvent.click(screen.getByRole('button', { name: 'Supprimer définitivement' }))
  await waitFor(() => expect(remove).toHaveBeenCalledWith(7))
  expect(await screen.findByText('Registre')).toBeTruthy()
})

it('Annuler ou Échap ferme sans rien supprimer', async () => {
  open()
  fireEvent.click(await screen.findByRole('button', { name: 'Supprimer' }))
  await screen.findByRole('alertdialog')
  // L'écoute d'Échap est posée par le même effet que le focus : l'attendre avant d'appuyer.
  await waitFor(() => expect(document.activeElement.textContent).toBe('Annuler'))
  fireEvent.keyDown(window, { key: 'Escape' })
  await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull())
  expect(remove).not.toHaveBeenCalled()
})
