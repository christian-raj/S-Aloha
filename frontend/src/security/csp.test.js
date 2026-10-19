import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { expect, it } from 'vitest'

// S5 (ADR-0018) : la CSP de nginx autorise le script de thème en ligne par son empreinte.
// Modifier ce script sans mettre l'empreinte à jour bloquerait le thème en production.
it('l\'empreinte CSP de nginx correspond au script en ligne d\'index.html', () => {
  const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')
  const scripts = [...html.matchAll(/<script\b[^>]*>([\s\S]*?)<\/script\s*>/gi)].filter(m => !/\bsrc=/i.test(m[0])).map(m => m[1])
  expect(scripts).toHaveLength(1)
  const hash = 'sha256-' + createHash('sha256').update(scripts[0]).digest('base64')
  const nginx = readFileSync(resolve(process.cwd(), 'nginx.conf'), 'utf8')
  expect(nginx).toContain(`'${hash}'`)
})
