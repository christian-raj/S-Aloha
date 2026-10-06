import { describe, expect, it } from 'vitest'
import { toForm, toPayload } from './RecordForm'

const fields = [
  { key: 'title' },
  { key: 'dueDate', type: 'date' },
  { key: 'serviceId', type: 'select', numeric: true, required: true },
  { key: 'risk', type: 'select', options: ['Faible'] },
  { key: 'availabilityTarget', type: 'number' },
  { key: 'isMajor', type: 'checkbox' },
  { key: 'ownerId', nameKey: 'ownerDisplayName', typeKey: 'ownerType', type: 'person' },
]

describe('RecordForm — conversions API ↔ formulaire', () => {
  it('un enregistrement fait l\'aller-retour sans perte', () => {
    const record = {
      title: 'T', dueDate: '2026-10-12T00:00:00Z', serviceId: 3, risk: 'Faible', availabilityTarget: 99.5,
      isMajor: true, ownerId: 'fara.rasoa', ownerDisplayName: 'Fara Rasoa', ownerType: 'User'
    }
    const form = toForm(fields, record)
    expect(form.dueDate).toBe('2026-10-12')
    const p = toPayload(fields, form)
    expect(p).toMatchObject({
      title: 'T', serviceId: 3, risk: 'Faible', availabilityTarget: 99.5, isMajor: true,
      ownerId: 'fara.rasoa', ownerDisplayName: 'Fara Rasoa', ownerType: 'User'
    })
    expect(p.dueDate.slice(0, 10)).toBe('2026-10-12')
  })

  it('les champs vides partent à null, pas en chaîne vide', () => {
    const p = toPayload(fields, toForm(fields, { title: 'T' }))
    expect(p).toMatchObject({ dueDate: null, risk: null, availabilityTarget: null, ownerId: null, isMajor: false })
  })
})
