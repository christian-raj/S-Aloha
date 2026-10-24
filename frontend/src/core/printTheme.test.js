import { afterEach, expect, it } from 'vitest'
import { printInLightTheme } from './printTheme'

let stop
afterEach(() => { stop?.(); document.documentElement.className = '' })

it('imprime en thème clair et rend ensuite le thème sombre choisi', () => {
  document.documentElement.classList.add('dark')
  stop = printInLightTheme()
  window.dispatchEvent(new Event('beforeprint'))
  expect(document.documentElement.classList.contains('dark')).toBe(false)
  window.dispatchEvent(new Event('afterprint'))
  expect(document.documentElement.classList.contains('dark')).toBe(true)
})

it('laisse le thème clair tel quel', () => {
  stop = printInLightTheme()
  window.dispatchEvent(new Event('beforeprint'))
  window.dispatchEvent(new Event('afterprint'))
  expect(document.documentElement.classList.contains('dark')).toBe(false)
})
