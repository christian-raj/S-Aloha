import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ChangeImpact } from './ChangeDetail'

let impact
vi.mock('../../../api', () => ({ api: { configurationItems: { impactOf: () => Promise.resolve(impact) } } }))
afterEach(cleanup)

describe("Impact d'un changement (CFG-20)", () => {
  it('liste les CI modifiés, ceux touchés en aval et les services', async () => {
    impact = {
      cis: [{ id: 1, reference: 'CI-2026-0001', title: 'srv-erp', ciType: 'Serveur', status: 'En service' }],
      downstream: [
        { id: 2, reference: 'CI-2026-0002', title: 'app-paie', depth: 1, relation: 'Dépend de', via: { reference: 'CI-2026-0001' } },
        { id: 0, reference: "CI d'un autre périmètre", title: "CI d'un autre périmètre", depth: 2, relation: 'Dépend de', via: { reference: 'CI-2026-0002' } }],
      services: [{ id: 5, reference: 'SVC-2026-0005', title: 'Paie', status: 'En service', ownerDisplayName: 'RH' }],
    }
    render(<MemoryRouter><ChangeImpact change={{ id: 9 }} /></MemoryRouter>)
    expect(await screen.findByText('Touchés en aval (2)')).toBeTruthy()
    expect(screen.getByText('Services touchés (1)')).toBeTruthy()
    expect(screen.getByText('SVC-2026-0005')).toBeTruthy()
  })

  it('sans CI relié, invite à relier les CI', async () => {
    impact = { cis: [], downstream: [], services: [] }
    render(<MemoryRouter><ChangeImpact change={{ id: 9 }} /></MemoryRouter>)
    expect(await screen.findByText(/Aucun CI relié/)).toBeTruthy()
  })
})
