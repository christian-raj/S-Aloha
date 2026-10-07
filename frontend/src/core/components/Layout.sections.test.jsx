import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Layout from './Layout'

vi.mock('../../api', () => ({ getUser: () => ({ username: 'a', displayName: 'Admin', role: 'Admin' }) }))

afterEach(cleanup)

// Registre réel : Paramétrage et Administration n'ont encore aucune page.
// Même pour un Admin, une section vide ne s'affiche pas (ni titre ni filet).
it('aucune section vide, même pour un Admin', () => {
  render(<MemoryRouter><Routes><Route path="*" element={<Layout />} /></Routes></MemoryRouter>)
  const titles = [...document.querySelectorAll('.sidebar .nav-section')].map((p) => p.textContent)
  expect(titles).toEqual(['Pilotage', 'Pratiques'])
})
