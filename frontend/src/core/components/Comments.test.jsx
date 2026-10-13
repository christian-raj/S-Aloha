import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import Comments from './Comments'

let canWriteNotes = true
const create = vi.fn(() => Promise.resolve({}))
vi.mock('../../api', () => ({
  getUser: () => ({ username: 'fara.rasoa', role: 'User' }),
  api: { comments: {
    list: () => Promise.resolve({ canWriteNotes, comments: [
      { id: 1, author: 'tiana', authorDisplayName: 'Tiana', visibility: 'Public', body: 'Bonjour', createdAt: '2026-10-13T08:00:00Z' },
      { id: 2, author: 'fara.rasoa', authorDisplayName: 'Fara', visibility: 'Note', body: 'Piste : DNS', createdAt: '2026-10-13T09:00:00Z' }] }),
    create: (...a) => create(...a), remove: vi.fn(),
  } },
}))

afterEach(() => { cleanup(); create.mockClear(); canWriteNotes = true })

describe('Commentaires (SOC-24)', () => {
  it('marque les notes de travail ; seul son auteur retire un commentaire', async () => {
    render(<Comments type="incident" id={7} />)
    expect(await screen.findByText('Piste : DNS')).toBeTruthy()
    expect(screen.getByText('Note de travail')).toBeTruthy()
    expect(screen.getAllByTitle('Retirer le commentaire')).toHaveLength(1)
  })

  it('publie une note de travail quand on y a droit', async () => {
    render(<Comments type="incident" id={7} />)
    fireEvent.click(await screen.findByLabelText('Note de travail (équipe)'))
    fireEvent.change(screen.getByLabelText('Ajouter un commentaire'), { target: { value: 'Licence commandée' } })
    fireEvent.click(screen.getByRole('button', { name: 'Publier' }))
    await waitFor(() => expect(create).toHaveBeenCalledWith({ type: 'incident', id: 7, visibility: 'Note', body: 'Licence commandée' }))
  })

  it("sans droit aux notes, pas d'option note de travail", async () => {
    canWriteNotes = false
    render(<Comments type="request" id={3} />)
    await screen.findByText('Bonjour')
    expect(screen.queryByLabelText('Note de travail (équipe)')).toBeNull()
  })
})
