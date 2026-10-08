import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Layout from './Layout'

let role = 'Admin'
vi.mock('../../api', () => ({ getUser: () => ({ username: 'u', displayName: 'U', role }) }))

afterEach(cleanup)

const titles = () => [...document.querySelectorAll('.sidebar .nav-section')].map((p) => p.textContent)

// Registre réel. Paramétrage n'a qu'une entrée, « Référentiel NIS 2 »,
// réservée à l'Admin ; Administration n'en a encore aucune. Une section sans
// entrée visible pour le rôle ne s'affiche pas (ni titre ni filet).
it.each([
  ['Admin', ['Pilotage', 'Pratiques', 'Paramétrage']],
  ['Manager', ['Pilotage', 'Pratiques']],
  ['User', ['Pilotage', 'Pratiques']],
])('registre réel : un %s voit les sections %j, jamais une section vide', (r, expected) => {
  role = r
  render(<MemoryRouter><Routes><Route path="*" element={<Layout />} /></Routes></MemoryRouter>)
  expect(titles()).toEqual(expected)
})
