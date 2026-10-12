import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Lifecycle } from './ArticleDetail'
import { KnownErrorArticle } from '../../problem-management/pages/ProblemDetail'

let role = 'User'
const submit = vi.fn(() => Promise.resolve({}))
const create = vi.fn(() => Promise.resolve({ id: 42, reference: 'KB-2026-0042' }))
const link = vi.fn(() => Promise.resolve({}))
let links = []
vi.mock('../../../api', () => ({
  getUser: () => ({ username: 'fara.rasoa', displayName: 'Fara', role }),
  api: {
    knowledge: {
      usage: () => Promise.resolve({ incidents: 3, requests: 1 }),
      submit: (...a) => submit(...a), review: vi.fn(), requestArchive: vi.fn(), create: (...a) => create(...a),
    },
    links: { list: () => Promise.resolve(links), create: (...a) => link(...a) },
  },
}))

afterEach(() => { cleanup(); submit.mockClear(); create.mockClear(); link.mockClear(); role = 'User'; links = [] })

const draft = { id: 5, status: 'Brouillon', ownerId: null, ownerType: null }

describe("Cycle de l'article", () => {
  it('KB-21 : un rédacteur soumet son brouillon ; KB-14 : utilisation affichée', async () => {
    render(<Lifecycle article={draft} onSaved={() => {}} />)
    expect(await screen.findByText(/3 incident\(s\) et 1 demande\(s\)/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Soumettre à publication' }))
    await waitFor(() => expect(submit).toHaveBeenCalledWith(5))
  })

  it("KB-21 : un gestionnaire publie directement, sans bouton de soumission", () => {
    role = 'Manager'
    render(<Lifecycle article={draft} onSaved={() => {}} />)
    expect(screen.queryByRole('button', { name: 'Soumettre à publication' })).toBeNull()
  })

  it('KB-17 : seul le responsable (ou un gestionnaire) voit la revue', () => {
    const published = { id: 6, status: 'Publié', reviewDate: '2020-01-01T00:00:00Z' }
    render(<Lifecycle article={{ ...published, ownerType: 'User', ownerId: 'autre.personne' }} onSaved={() => {}} />)
    expect(screen.queryByRole('button', { name: "Confirmer l'article" })).toBeNull()
    cleanup()
    render(<Lifecycle article={{ ...published, ownerType: 'User', ownerId: 'Fara.Rasoa' }} onSaved={() => {}} />)
    expect(screen.getByRole('button', { name: "Confirmer l'article" })).toBeTruthy()
    expect(screen.getByText(/échue/)).toBeTruthy()
  })
})

describe("Article d'erreur connue (PRB-30, KB-12)", () => {
  const problem = { id: 9, reference: 'PRB-2026-0009', title: 'VPN instable', status: 'Erreur connue',
    description: 'Coupures', knownErrorWorkaround: 'Relancer le client', category: 'Réseau', affectedService: 'VPN' }

  it("crée l'article depuis le problème et le relie", async () => {
    render(<MemoryRouter><KnownErrorArticle p={problem} /></MemoryRouter>)
    fireEvent.click(await screen.findByRole('button', { name: "Créer l'article d'erreur connue" }))
    await waitFor(() => expect(link).toHaveBeenCalledWith({ fromType: 'problem', fromId: 9, toReference: 'KB-2026-0042' }))
    const dto = create.mock.calls[0][0]
    expect(dto.articleType).toBe('Erreur connue')
    expect(dto.content).toContain('Relancer le client')
  })

  it("renvoie à l'article quand il existe déjà", async () => {
    links = [{ type: 'article', id: 3, reference: 'KB-2026-0003', title: 'VPN instable', status: 'Publié' }]
    render(<MemoryRouter><KnownErrorArticle p={problem} /></MemoryRouter>)
    expect(await screen.findByText('KB-2026-0003')).toBeTruthy()
    expect(screen.queryByRole('button')).toBeNull()
  })
})
