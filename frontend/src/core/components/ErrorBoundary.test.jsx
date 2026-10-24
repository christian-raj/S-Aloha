import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Layout from './Layout'
import ErrorBoundary from './ErrorBoundary'

vi.mock('../../api', () => ({ api: {}, getUser: () => ({ username: 'u', displayName: 'U', role: 'User' }) }))

const Boom = () => { throw new Error('api.me.mailPreferences is not a function') }

// React journalise l'erreur attrapée : sans intérêt dans la sortie des tests.
beforeEach(() => vi.spyOn(console, 'error').mockImplementation(() => {}))
afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('une page qui plante affiche un message, la navigation reste là, et changer de page réarme la garde', () => {
  render(
    <MemoryRouter initialEntries={['/preferences']}>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route path="preferences" element={<Boom />} />
          <Route path="reports" element={<p>Tableau de bord</p>} />
        </Route>
      </Routes>
    </MemoryRouter>
  )
  const alert = screen.getByRole('alert')
  expect(within(alert).getByText('Cette page n’a pas pu s’afficher')).toBeTruthy()
  expect(within(alert).getByText('api.me.mailPreferences is not a function')).toBeTruthy()

  const sidebar = document.querySelector('.sidebar')
  fireEvent.click(within(sidebar).getByRole('link', { name: 'Reporting' }))
  expect(screen.queryByRole('alert')).toBeNull()
  expect(screen.getByText('Tableau de bord')).toBeTruthy()
})

it('la garde racine remplace la page blanche', () => {
  render(<ErrorBoundary level="app"><Boom /></ErrorBoundary>)
  expect(screen.getByText('S-Aloha n’a pas pu s’afficher')).toBeTruthy()
  expect(screen.getByRole('button', { name: 'Recharger la page' })).toBeTruthy()
})
