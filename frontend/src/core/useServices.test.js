import { describe, expect, it } from 'vitest'
import { withServices } from './useServices'

const services = [{ id: 1, title: 'Messagerie', status: 'En service' }, { id: 2, title: 'Fax', status: 'Retiré' }]

describe('Service du catalogue (SLM-10)', () => {
  it('remplace le texte libre par le choix du service', () => {
    const c = withServices({ fields: [{ key: 'title' }, { key: 'affectedService' }] }, services)
    expect(c.fields.map(f => f.key)).toEqual(['title', 'serviceId'])
    expect(c.fields[1].options).toEqual([{ value: 1, label: 'Messagerie' }, { value: 2, label: 'Fax (retiré)' }])
    expect(c.fields[1].numeric).toBe(true)
  })

  it('ajoute le choix du service après le champ indiqué', () => {
    const c = withServices({ fields: [{ key: 'title' }, { key: 'requestedItem' }, { key: 'owner' }] }, services, 'requestedItem')
    expect(c.fields.map(f => f.key)).toEqual(['title', 'requestedItem', 'serviceId', 'owner'])
  })
})
