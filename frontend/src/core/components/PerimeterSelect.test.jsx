import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import PerimeterSelect from './PerimeterSelect'
import { perimeterName } from '../usePerimeters'

afterEach(cleanup)

const two = [{ id: 2, name: 'Siège', role: 'Manager' }, { id: 3, name: 'Usine', role: 'User' }]

describe('Choix du périmètre (PER-04, PER-07)', () => {
  it("masqué quand l'utilisateur n'a qu'un périmètre", () => {
    const { container } = render(<PerimeterSelect perimeters={[two[0]]} value={null} onChange={() => {}} required />)
    expect(container.innerHTML).toBe('')
  })

  it('obligatoire avec plusieurs périmètres, et renvoie un identifiant numérique', () => {
    const onChange = vi.fn()
    render(<PerimeterSelect perimeters={two} value={null} onChange={onChange} required />)
    expect(screen.getByText('— Choisir —')).toBeTruthy()
    fireEvent.change(screen.getByRole('combobox'), { target: { value: '3' } })
    expect(onChange).toHaveBeenCalledWith(3)
  })

  it('nomme un périmètre, ou à défaut son numéro', () => {
    expect(perimeterName(two, 3)).toBe('Usine')
    expect(perimeterName(two, 9)).toBe('Périmètre n° 9')
  })
})
