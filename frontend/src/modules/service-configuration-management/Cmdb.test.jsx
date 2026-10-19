import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import RecordForm, { toForm, toPayload } from '../../core/components/RecordForm'
import { ciConfig } from './config'
import Staging from './pages/Staging'
import StagingAsset from './pages/StagingAsset'
import DiscoverySettings from './pages/DiscoverySettings'

let role = 'Manager'
let asset
const list = vi.fn(() => Promise.resolve([
  { id: 7, status: 'En conflit', hostname: 'srv-a', ipAddress: '10.0.0.7', detectedType: 'Serveur', sources: 'IpSweep',
    confidenceScore: 50, matchRule: 'IpAddress', unknown: false, lastSeenAt: '2026-10-19T08:00:00Z', matchedCi: { id: 3, reference: 'CI-2026-0003', title: 'srv-a' } },
]))
const reconcile = vi.fn(() => Promise.resolve({ configurationItem: { id: 3 } }))
const createCredential = vi.fn(() => Promise.resolve({}))
vi.mock('../../api', () => ({
  getUser: () => ({ username: 'chef', displayName: 'Chef', role }),
  api: {
    perimeters: { mine: () => Promise.resolve([]) },
    configurationItems: { list: () => Promise.resolve([]) },
    discovery: {
      staging: {
        summary: () => Promise.resolve({ toReconcile: 2, conflicts: 1, unknown: 1, reconciled: 4, ignored: 0 }),
        list: (...a) => list(...a), get: () => Promise.resolve(asset), reconcile: (...a) => reconcile(...a),
        ignore: vi.fn(), restore: vi.fn(), remove: vi.fn(),
      },
      jobs: { list: () => Promise.resolve([]), run: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn() },
      credentials: {
        list: () => Promise.resolve({ vaultConfigured: true, items: [
          { id: 1, name: 'Communauté du siège', credentialType: 'SnmpV2c', hasSecret: true, secretChangedAt: '2026-10-19T00:00:00Z', usedBy: 2 }] }),
        create: (...a) => createCredential(...a), update: vi.fn(), remove: vi.fn(),
      },
      endOfLife: { list: () => Promise.resolve([]) },
    },
  },
}))

afterEach(() => { cleanup(); list.mockClear(); reconcile.mockClear(); createCredential.mockClear() })

const at = (path, route, el) => render(<MemoryRouter initialEntries={[path]}><Routes><Route path={route} element={el} /></Routes></MemoryRouter>)

describe('Fiche CI : champs selon le type (CFG-43, CFG-44)', () => {
  const labels = () => [...document.querySelectorAll('.field > label, .form-heading')].map(l => l.firstChild.textContent)
  it('une application montre son portefeuille, un équipement son identification matérielle', () => {
    const { rerender } = render(<RecordForm fields={ciConfig.fields} form={{ ...toForm(ciConfig.fields), ciType: 'Application' }} onChange={() => {}} />)
    expect(labels()).toContain('Code applicatif')
    expect(labels()).toContain('Portefeuille applicatif')
    expect(labels()).not.toContain('Numéro de série')
    rerender(<RecordForm fields={ciConfig.fields} form={{ ...toForm(ciConfig.fields), ciType: 'Réseau' }} onChange={() => {}} />)
    expect(labels()).toContain('Numéro de série')
    expect(labels()).not.toContain('Code applicatif')
    expect(screen.getAllByText('Requis pour un CI en service ou hors service.').length).toBeGreaterThan(0)
  })
  it('les intertitres ne partent pas dans la requête', () => {
    const payload = toPayload(ciConfig.fields, toForm(ciConfig.fields, { ciType: 'Serveur', serialNumber: 'SN-1' }))
    expect(payload.serialNumber).toBe('SN-1')
    expect(Object.keys(payload).some(k => k.startsWith('_'))).toBe(false)
  })
})

describe('Sas de staging (CFG-40)', () => {
  it('les tuiles comptent et filtrent ; la ligne montre score et CI proposé', async () => {
    at('/configuration/staging', '/configuration/staging', <Staging />)
    expect(await screen.findByText('srv-a')).toBeTruthy()
    expect(screen.getByRole('tab', { name: /À traiter/ }).textContent).toContain('3')
    expect(screen.getByText('CI-2026-0003')).toBeTruthy()
    fireEvent.click(screen.getByRole('tab', { name: /En conflit/ }))
    await waitFor(() => expect(list).toHaveBeenLastCalledWith({ status: 'En conflit' }))
  })

  it('fiche : l’action principale suit la suggestion, et rapproche du CI proposé', async () => {
    asset = {
      asset: { id: 7, status: 'À rapprocher', hostname: 'srv-a', ipAddress: '10.0.0.7', detectedType: 'Serveur', sources: 'IpSweep',
        confidenceScore: 80, matchRule: 'Hostname', perimeterId: 1, inDirectory: true, unknown: false },
      suggestion: { score: 80, rule: 'Hostname', action: 'UpdateExisting', conflict: null },
      targetCi: { id: 3, reference: 'CI-2026-0003', title: 'srv-a', ciType: 'Serveur', status: 'En service', ipAddress: '10.0.0.9' },
      proposal: { title: 'srv-a', ciType: 'Serveur' },
    }
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    at('/configuration/staging/7', '/configuration/staging/:id', <StagingAsset />)
    const primary = await screen.findByRole('button', { name: 'Rapprocher de CI-2026-0003' })
    expect(primary.className).toBe('btn')
    expect(screen.getByRole('button', { name: 'Créer le CI' }).className).toBe('btn ghost')
    // L'écart d'adresse IP est signalé dans la comparaison.
    expect(document.querySelector('tr.diff')).toBeTruthy()
    fireEvent.click(primary)
    await waitFor(() => expect(reconcile).toHaveBeenCalledWith(7, { action: 'update', targetCiId: 3 }))
  })

  it('fiche : sans rapprochement, « Créer le CI » devient l’action principale', async () => {
    asset = { ...asset, suggestion: { score: 0, rule: 'None', action: 'CreateNew', conflict: null }, targetCi: null }
    at('/configuration/staging/7', '/configuration/staging/:id', <StagingAsset />)
    expect((await screen.findByRole('button', { name: 'Créer le CI' })).className).toBe('btn')
    expect(screen.queryByRole('button', { name: /Rapprocher de / })).toBeNull()
  })
})

describe('Paramétrage › Découverte du réseau (CFG-45, CFG-46)', () => {
  it('un gestionnaire n’a pas l’onglet du coffre', async () => {
    role = 'Manager'
    at('/settings/discovery', '/settings/discovery', <DiscoverySettings />)
    expect(await screen.findByText('Aucune tâche de scan')).toBeTruthy()
    expect(screen.queryByRole('tab', { name: 'Identifiants (coffre)' })).toBeNull()
  })

  it('l’Admin enregistre une communauté SNMP ; le secret n’est jamais réaffiché', async () => {
    role = 'Admin'
    at('/settings/discovery?tab=credentials', '/settings/discovery', <DiscoverySettings />)
    expect(await screen.findByText('Communauté du siège')).toBeTruthy()
    expect(screen.getByText(/Enregistré, chiffré/)).toBeTruthy()
    // Utilisé par des tâches : la suppression est bloquée.
    expect(screen.getByRole('button', { name: 'Supprimer' }).disabled).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: '+ Nouvel identifiant' }))
    fireEvent.change(screen.getByPlaceholderText('Ex. : Communauté SNMP du siège'), { target: { value: 'Agences' } })
    const secret = document.querySelector('input[type=password]')
    expect(secret).toBeTruthy()
    fireEvent.change(secret, { target: { value: 'c0mmun4ute' } })
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }))
    await waitFor(() => expect(createCredential).toHaveBeenCalledWith(expect.objectContaining({
      name: 'Agences', credentialType: 'SnmpV2c', secret: 'c0mmun4ute' })))
  })
})
