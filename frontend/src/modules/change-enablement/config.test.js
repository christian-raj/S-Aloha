import { describe, expect, it, vi } from 'vitest'

vi.mock('../../api', () => ({ api: {} }))
import { changeConfig } from './config'

describe('Changements : dates réelles et revue (CHG-14, CHG-15)', () => {
  it('propose les dates réelles et la revue sur la fiche seulement', () => {
    const keys = changeConfig.fields.map(f => f.key)
    expect(keys).toEqual(expect.arrayContaining(['actualStart', 'actualEnd', 'pirNotes']))
    expect(changeConfig.fields.filter(f => ['actualStart', 'actualEnd', 'pirNotes'].includes(f.key)).every(f => f.create === false)).toBe(true)
  })

  it('signale un dépassement de la fin planifiée', () => {
    const meta = changeConfig.meta({ overrun: true, plannedEnd: '2026-10-20T18:00:00Z', actualEnd: '2026-10-20T21:00:00Z' }).filter(Boolean)
    expect(meta.some(m => m.startsWith('Dépassement'))).toBe(true)
    expect(changeConfig.meta({ overrun: false }).filter(Boolean)).toEqual([])
  })
})
