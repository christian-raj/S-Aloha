import { describe, expect, it } from 'vitest'
import { reachable } from './useTransitions'

const graph = { Nouveau: ['En cours', 'Résolu'], Clos: [] }

describe('reachable — statuts proposés (SOC-05)', () => {
  it('propose le statut courant et ses successeurs seulement', () => {
    expect(['Nouveau', 'En cours', 'En attente', 'Résolu', 'Clos'].filter(s => reachable(graph, 'Nouveau', s)))
      .toEqual(['Nouveau', 'En cours', 'Résolu'])
  })
  it('un statut final ne propose que lui-même', () => {
    expect(['Nouveau', 'Clos'].filter(s => reachable(graph, 'Clos', s))).toEqual(['Clos'])
  })
  it('sans graphe (pas encore chargé), tout reste proposé', () => {
    expect(reachable(null, 'Nouveau', 'Clos')).toBe(true)
  })
})
