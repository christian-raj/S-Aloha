import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Layout from './Layout'

vi.mock('../../api', () => ({ api: {}, getUser: () => ({ username: 'u', displayName: 'U', role: 'User' }) }))

beforeEach(() => localStorage.clear())
afterEach(cleanup)

const renderAt = (url) => render(
  <MemoryRouter initialEntries={[url]}><Routes><Route path="*" element={<Layout />} /></Routes></MemoryRouter>
)
const sidebar = () => document.querySelector('.sidebar')
const group = (label) => within(sidebar()).getByRole('button', { name: label })
const items = (label) => document.getElementById(group(label).getAttribute('aria-controls'))

it('NIS 2 est rangé dans « Conformité & réglementation », pas dans les pratiques ITIL', () => {
  renderAt('/')
  expect(within(items('Conformité & réglementation')).getByText('NIS 2')).toBeTruthy()
  expect(within(items('Pratiques')).queryByText('NIS 2')).toBeNull()
  expect(within(items('Pratiques')).getByText('Incidents')).toBeTruthy()
})

it('une section se replie et se déplie, et ce choix est mémorisé', () => {
  renderAt('/')
  fireEvent.click(group('Pratiques'))
  expect(group('Pratiques').getAttribute('aria-expanded')).toBe('false')
  expect(items('Pratiques')).toBeNull()
  expect(JSON.parse(localStorage.getItem('s-aloha-nav-collapsed'))).toEqual(['practices'])

  cleanup()
  renderAt('/')
  expect(group('Pratiques').getAttribute('aria-expanded')).toBe('false')
  fireEvent.click(group('Pratiques'))
  expect(within(items('Pratiques')).getByText('Incidents')).toBeTruthy()
})

it('la section de la page ouverte reste dépliée, même repliée auparavant', () => {
  localStorage.setItem('s-aloha-nav-collapsed', JSON.stringify(['compliance']))
  renderAt('/assessments/3')
  const button = group('Conformité & réglementation')
  expect(button.getAttribute('aria-expanded')).toBe('true')
  fireEvent.click(button)
  expect(button.getAttribute('aria-expanded')).toBe('true')
  expect(within(items('Conformité & réglementation')).getByText('NIS 2').closest('a').className).toContain('active')
})

it('reste utilisable sans stockage local (navigation privée)', () => {
  const spy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('bloqué') })
  try {
    renderAt('/')
    expect(group('Pratiques').getAttribute('aria-expanded')).toBe('true')
  } finally { spy.mockRestore() }
})
