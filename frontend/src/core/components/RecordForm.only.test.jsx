import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import RecordForm from './RecordForm'

vi.mock('../../api', () => ({ getUser: () => ({ role: 'User' }), api: {} }))
afterEach(cleanup)

const fields = [
  { key: 'title', label: 'Titre' },
  { key: 'originReference', label: "Enregistrement d'origine", onlyCreate: true },
  { key: 'value', label: 'Valeur', create: false },
]

describe('Champs selon le formulaire', () => {
  it('à la création : champ de proposition, sans les champs de suite', () => {
    render(<RecordForm fields={fields} form={{ title: '', originReference: '', value: '' }} onChange={() => {}} creating />)
    expect(screen.getByText("Enregistrement d'origine")).toBeTruthy()
    expect(screen.queryByText('Valeur')).toBeNull()
  })
  it('sur la fiche : sans le champ de proposition', () => {
    render(<RecordForm fields={fields} form={{ title: '', originReference: '', value: '' }} onChange={() => {}} />)
    expect(screen.queryByText("Enregistrement d'origine")).toBeNull()
    expect(screen.getByText('Valeur')).toBeTruthy()
  })
})
