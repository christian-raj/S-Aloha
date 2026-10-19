import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Layout from './Layout'

let role = 'Admin'
vi.mock('../../api', () => ({ api: {}, getUser: () => ({ username: 'u', displayName: 'U', role }) }))

afterEach(cleanup)

const titles = () => [...document.querySelectorAll('.sidebar .nav-section')].map((p) => p.textContent)

// Registre réel. Paramétrage s'ouvre aux gestionnaires avec « Découverte du
// réseau » (CFG-46) ; « Référentiel NIS 2 » et Administration restent à
// l'Admin. Une section sans entrée visible pour le rôle ne s'affiche pas (ni
// titre ni filet) : un User ne voit donc ni Paramétrage ni Administration.
it.each([
  ['Admin', ['Pilotage', 'Pratiques', 'Projets', 'Conformité & réglementation', 'Paramétrage', 'Administration']],
  ['Manager', ['Pilotage', 'Pratiques', 'Projets', 'Conformité & réglementation', 'Paramétrage']],
  ['User', ['Pilotage', 'Pratiques', 'Projets', 'Conformité & réglementation']],
])('registre réel : un %s voit les sections %j, jamais une section vide', (r, expected) => {
  role = r
  render(<MemoryRouter><Routes><Route path="*" element={<Layout />} /></Routes></MemoryRouter>)
  expect(titles()).toEqual(expected)
})
