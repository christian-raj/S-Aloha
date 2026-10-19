import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Staging from './Staging'

const accept = vi.fn(() => Promise.resolve({}))
const side = (id, hostname, ci) => ({ id, hostname, ipAddress: null, status: ci ? 'Rapproché' : 'À rapprocher', ci })
vi.mock('../../../api', () => ({
  api: { discovery: {
    staging: { summary: () => Promise.resolve({ toReconcile: 1, conflicts: 0, unknown: 0, reconciled: 2, ignored: 0, relations: 2 }), list: () => Promise.resolve([]) },
    relations: {
      list: () => Promise.resolve([
        { id: 1, type: 'Héberge', reason: 'Proxmox VE : VM 101 sur le nœud pve1', lastSeenAt: '2026-10-19T08:00:00Z', ready: true,
          source: side(10, 'pve1', { id: 5, reference: 'CI-2026-0005', title: 'pve1' }), target: side(11, 'srv-web', { id: 6, reference: 'CI-2026-0006', title: 'srv-web' }) },
        { id: 2, type: 'Héberge', reason: 'Proxmox VE : conteneur 200 sur le nœud pve1', lastSeenAt: '2026-10-19T08:00:00Z', ready: false,
          source: side(10, 'pve1', { id: 5, reference: 'CI-2026-0005', title: 'pve1' }), target: side(12, 'ct-dns', null) },
      ]),
      accept: (...a) => accept(...a), dismiss: () => Promise.resolve({})
    }
  } }
}))
afterEach(() => { cleanup(); accept.mockClear() })

it('CFG-55 : relations proposées, acceptables une fois les deux actifs rapprochés', async () => {
  render(<MemoryRouter initialEntries={['/configuration/staging?view=relations']}><Staging /></MemoryRouter>)
  const ready = (await screen.findByText('Proxmox VE : VM 101 sur le nœud pve1')).closest('tr')
  const waiting = screen.getByText('Proxmox VE : conteneur 200 sur le nœud pve1').closest('tr')
  expect(within(waiting).getByRole('button', { name: 'Accepter' }).disabled).toBe(true)
  expect(within(waiting).getByText(/non rapproché/)).toBeTruthy()
  fireEvent.click(within(ready).getByRole('button', { name: 'Accepter' }))
  await waitFor(() => expect(accept).toHaveBeenCalledWith(1))
})
