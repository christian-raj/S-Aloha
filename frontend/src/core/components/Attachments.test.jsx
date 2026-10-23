import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import Attachments from './Attachments'

let enabled = true
const upload = vi.fn(() => Promise.resolve({}))
const remove = vi.fn(() => Promise.resolve(null))
vi.mock('../../api', () => ({
  getUser: () => ({ username: 'agent', role: 'User' }),
  api: { attachments: {
    list: () => Promise.resolve({ enabled, maxFileMb: 1, extensions: ['.pdf', '.png'], items: [
      { id: 1, fileName: 'rapport.pdf', contentType: 'application/pdf', size: 2048, createdBy: 'agent', createdByDisplayName: 'Agent', createdAt: '2026-10-23T08:00:00Z' },
      { id: 2, fileName: 'capture.png', contentType: 'image/png', size: 10, createdBy: 'autre', createdByDisplayName: 'Autre', createdAt: '2026-10-23T09:00:00Z' }] }),
    upload: (...a) => upload(...a), remove: (...a) => remove(...a), contentUrl: id => `/api/attachments/${id}/content` } }
}))
afterEach(() => { cleanup(); vi.clearAllMocks(); enabled = true })

it('SOC-25 : liste, téléchargement, retrait par l’auteur seulement, dépôt borné en taille', async () => {
  render(<Attachments type="incident" id={7} />)
  expect((await screen.findByText('rapport.pdf')).closest('a').getAttribute('href')).toBe('/api/attachments/1/content')
  expect(screen.getAllByText('Retirer')).toHaveLength(1)
  fireEvent.click(screen.getByText('Retirer'))
  await waitFor(() => expect(remove).toHaveBeenCalledWith(1))
  const big = new File([new Uint8Array(2 * 1024 * 1024)], 'gros.pdf', { type: 'application/pdf' })
  fireEvent.change(screen.getByLabelText('Fichier à joindre'), { target: { files: [big] } })
  expect(await screen.findByText(/1 Mo au plus\./)).toBeTruthy()
  expect(upload).not.toHaveBeenCalled()
  const ok = new File(['%PDF-1.7'], 'ok.pdf', { type: 'application/pdf' })
  fireEvent.change(screen.getByLabelText('Fichier à joindre'), { target: { files: [ok] } })
  await waitFor(() => expect(upload).toHaveBeenCalledWith('incident', 7, ok))
})

it('SOC-25 : sans stockage activé, l’onglet le dit et ne propose pas de dépôt', async () => {
  enabled = false
  render(<Attachments type="incident" id={7} />)
  expect(await screen.findByText(/non activées/)).toBeTruthy()
  expect(screen.queryByLabelText('Fichier à joindre')).toBeNull()
})
