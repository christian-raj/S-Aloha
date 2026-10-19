import React from 'react'
import { afterEach, expect, it } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import CategoryInput from './CategoryInput'
import { withCategories } from '../useCategories'

afterEach(cleanup)

const categories = [{ name: 'Réseau', active: true }, { name: 'Ancienne', active: false }]

it('ADM-51 : liste des catégories actives, la valeur hors liste reste affichée', () => {
  render(<CategoryInput categories={categories} value="Ancienne" onChange={() => {}} />)
  const select = screen.getByLabelText('Catégorie')
  expect(select.tagName).toBe('SELECT')
  expect([...select.options].map(o => o.textContent)).toEqual(['—', 'Ancienne (hors liste)', 'Réseau'])
  expect(select.value).toBe('Ancienne')
})

it('ADM-51 : sans liste, saisie libre', () => {
  render(<CategoryInput categories={[]} value="Libre" onChange={() => {}} />)
  expect(screen.getByLabelText('Catégorie').tagName).toBe('INPUT')
})

it('ADM-51 : le champ de formulaire devient une liste dès qu’une catégorie est active', () => {
  const config = { fields: [{ key: 'title' }, { key: 'category', label: 'Catégorie', placeholder: 'x' }] }
  expect(withCategories(config, [])).toBe(config)
  const field = withCategories(config, categories).fields[1]
  expect(field.type).toBe('select')
  expect(field.options).toEqual(['Réseau'])
})
