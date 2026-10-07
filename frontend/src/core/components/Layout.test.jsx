import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

let role = 'User'
vi.mock('../../api', () => ({ getUser: () => ({ username: 'u', displayName: 'Utilisateur', role }) }))

// Registre réel, plus une sous-entrée de paramétrage réservée aux gestionnaires :
// aucune entrée réelle n'utilise encore `roles` (les pages 🔜 l'utiliseront).
vi.mock('../../modules/registry', async (importOriginal) => {
  const real = await importOriginal()
  return {
    ...real,
    MODULES: real.MODULES.map((m) => m.id !== 'change' ? m : {
      ...m, pages: [...m.pages, { href: '/changes/models', label: 'Modèles (test)', roles: ['Manager', 'Admin'] }]
    }),
  }
})

const { default: Layout } = await import('./Layout')

const renderAt = (url) => render(
  <MemoryRouter initialEntries={[url]}>
    <Routes><Route path="*" element={<Layout />} /></Routes>
  </MemoryRouter>
)
const activeSublinks = () => [...document.querySelectorAll('.sidebar .nav-sublink.active')].map((a) => a.textContent)
const currentSublinks = () => [...document.querySelectorAll('.sidebar .nav-sublink[aria-current="page"]')].map((a) => a.textContent)

afterEach(cleanup)

describe('Barre latérale — navigation cible', () => {
  it('les sous-entrées réservées sont masquées pour un User, visibles pour un Manager', () => {
    role = 'User'
    renderAt('/changes')
    expect(screen.queryAllByText('Modèles (test)')).toHaveLength(0)
    cleanup()

    role = 'Manager'
    renderAt('/changes')
    expect(screen.getAllByText('Modèles (test)').length).toBeGreaterThan(0)
  })

  it('« Erreurs connues » est la seule sous-entrée active sur le registre filtré', () => {
    role = 'User'
    renderAt('/problems?' + new URLSearchParams({ status: 'Erreur connue' }))
    expect(activeSublinks()).toEqual(['Erreurs connues'])
    expect(currentSublinks()).toEqual(['Erreurs connues'])
  })

  it('le registre reste actif avec un autre filtre, et sur une fiche', () => {
    role = 'User'
    renderAt('/problems?status=Nouveau')
    expect(activeSublinks()).toEqual(['Registre des problèmes'])
    cleanup()
    renderAt('/problems/12')
    expect(activeSublinks()).toEqual(['Registre des problèmes'])
  })
})
