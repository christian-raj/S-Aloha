import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Users from './Users'

vi.mock('../../../api', () => ({
  api: { admin: { users: () => Promise.resolve({
    mapping: [{ role: 'Admin', group: 'GRP-SALOHA-ADMINS' }, { role: 'Manager', group: 'GRP-SALOHA-MANAGERS' }, { role: 'User', group: 'GRP-SALOHA-USERS' }],
    users: [
      { username: 'tiana', displayName: 'Tiana', role: 'User', roleGroups: [], lastLoginAt: '2026-10-18T08:00:00Z',
        perimeters: [{ id: 2, name: 'Usine', role: 'Manager', via: ['groupe Équipe usine'] }] },
      { username: 'hery', displayName: 'Hery', role: 'Admin', roleGroups: [], lastLoginAt: null, perimeters: null }],
    withoutAccess: [{ id: 9, perimeter: 'Usine', principalId: 'parti', principalDisplayName: 'Parti', role: 'User' }] }) } }
}))
afterEach(cleanup)

it('ADM-10 à ADM-12 : rôle par périmètre et sa source, portée globale, connexion, comptes sans accès', async () => {
  render(<MemoryRouter><Users /></MemoryRouter>)
  expect(await screen.findByText('Tiana')).toBeTruthy()
  expect(screen.getByText(/groupe Équipe usine/)).toBeTruthy()
  expect(screen.getByText('Tous (portée globale)')).toBeTruthy()
  expect(screen.getByText(/Jamais connecté/)).toBeTruthy()
  expect(screen.getByRole('status').textContent).toContain('1 affectation(s) « sans accès »')
})
