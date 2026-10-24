import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Layout from './Layout'
import { MODULES, SECTIONS } from '../../modules/registry'
import { MODULE_ICONS, SECTION_ICONS } from './icons'

vi.mock('../../api', () => ({ api: {}, getUser: () => ({ username: 'u', displayName: 'U', role: 'Admin' }) }))

beforeEach(() => localStorage.clear())
afterEach(cleanup)

const renderAt = (url) => render(
  <MemoryRouter initialEntries={[url]}><Routes><Route path="*" element={<Layout />} /></Routes></MemoryRouter>
)
const sidebar = () => document.querySelector('.sidebar')

it('chaque entrée de la navigation a sa propre icône, sans doublon', () => {
  const icons = [
    ...MODULES.map((m) => MODULE_ICONS[m.id]),
    ...SECTIONS.flatMap((s) => s.entries.map((e) => SECTION_ICONS[e.icon])),
  ]
  expect(icons.every(Boolean)).toBe(true)
  expect(new Set(icons).size).toBe(icons.length)
})

it('la barre se réduit en rail, et ce choix est mémorisé', () => {
  renderAt('/')
  expect(document.querySelector('.shell').className).not.toContain('rail')
  fireEvent.click(within(sidebar()).getByRole('button', { name: 'Réduire le menu' }))
  expect(document.querySelector('.shell').className).toContain('rail')
  expect(localStorage.getItem('s-aloha-nav-rail')).toBe('1')

  cleanup()
  renderAt('/')
  expect(document.querySelector('.shell').className).toContain('rail')
  expect(within(sidebar()).getByRole('button', { name: 'Déployer le menu' })).toBeTruthy()
})

it('dans le rail, les sous-entrées passent dans un menu volant au survol', () => {
  localStorage.setItem('s-aloha-nav-rail', '1')
  renderAt('/')
  const link = within(sidebar()).getByRole('link', { name: 'Problèmes' })
  expect(within(sidebar()).queryByText('Erreurs connues')).toBeNull()
  fireEvent.mouseEnter(link.parentElement)
  const flyout = sidebar().querySelector('.rail-flyout')
  expect(within(flyout).getByText('Causes racines et erreurs connues')).toBeTruthy()
  expect(within(flyout).getByRole('link', { name: 'Erreurs connues' })).toBeTruthy()
  fireEvent.mouseLeave(link.parentElement)
  expect(sidebar().querySelector('.rail-flyout')).toBeNull()
})

it('dans le rail, une section repliée reste visible (le titre n\'est qu\'un filet)', () => {
  localStorage.setItem('s-aloha-nav-collapsed', JSON.stringify(['practices']))
  localStorage.setItem('s-aloha-nav-rail', '1')
  renderAt('/')
  expect(within(sidebar()).getByRole('link', { name: 'Incidents' })).toBeTruthy()
})

it('le tiroir mobile reste déployé, même avec le rail', () => {
  localStorage.setItem('s-aloha-nav-rail', '1')
  renderAt('/')
  fireEvent.click(screen.getByRole('button', { name: 'Ouvrir la navigation' }))
  const drawer = screen.getByRole('dialog', { name: 'Navigation' })
  expect(within(drawer).queryByRole('button', { name: /menu$/ })).toBeNull()
  fireEvent.click(within(drawer).getByRole('link', { name: 'Problèmes' }))
})

it('dans le rail, Paramétrage et Administration tiennent chacun en une icône à menu volant', () => {
  localStorage.setItem('s-aloha-nav-rail', '1')
  renderAt('/admin/health')
  expect(within(sidebar()).queryByRole('link', { name: 'Santé' })).toBeNull()
  const admin = within(sidebar()).getByRole('link', { name: 'Administration' })
  expect(admin.className).toContain('active')
  fireEvent.mouseEnter(admin.parentElement)
  const flyout = sidebar().querySelector('.rail-flyout')
  expect(within(flyout).getByRole('link', { name: 'Santé' }).getAttribute('aria-current')).toBe('page')
  expect(within(flyout).getByRole('link', { name: "Journal d'audit" })).toBeTruthy()
})
