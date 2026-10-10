import React from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import StatusField, { forceOptions } from './StatusField'

const statuses = [{ value: 'Nouveau' }, { value: 'En cours' }, { value: 'Clos' }]
const graph = { Nouveau: ['En cours'], 'En cours': ['Clos'], Clos: [] }
const options = () => [...document.querySelectorAll('option')].map(o => o.textContent)

afterEach(cleanup)

describe('StatusField — transitions et forçage (SOC-05)', () => {
  it('un non-Admin ne voit que le statut courant et ses successeurs', () => {
    render(<StatusField statuses={statuses} current="Nouveau" value="Nouveau" onChange={() => {}} graph={graph} isManager />)
    expect(options()).toEqual(['Nouveau', 'En cours'])
  })
  it('un Admin voit les autres statuts, marqués « transition forcée », et doit motiver', () => {
    render(<StatusField statuses={statuses} current="Nouveau" value="Clos" onChange={() => {}} graph={graph}
      isManager isAdmin reason="" onReason={() => {}} />)
    expect(options()).toEqual(['Nouveau', 'En cours', 'Clos (transition forcée)'])
    expect(screen.getByText('Motif de la transition forcée *')).toBeTruthy()
  })
  it('forceOptions : forçage seulement hors graphe', () => {
    expect(forceOptions(graph, 'Nouveau', 'En cours', 'x')).toBeUndefined()
    expect(forceOptions(graph, 'Nouveau', 'Clos', 'motif')).toEqual({ force: true, reason: 'motif' })
  })
})
