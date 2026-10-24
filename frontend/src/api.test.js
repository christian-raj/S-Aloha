import { expect, it } from 'vitest'
import { api } from './api'

// Les tests des pages simulent `api` : une clé déclarée deux fois dans le vrai
// module (la seconde écrase la première) leur échappe. Le 2026-10-24, `me`
// (préférences) était écrasé par `me()` (session) : « Mes préférences » restait vide.
it('le vrai module api expose la session et les préférences, sans collision', () => {
  expect(typeof api.me).toBe('function')
  expect(typeof api.preferences.mail).toBe('function')
  expect(typeof api.preferences.saveMail).toBe('function')
})
