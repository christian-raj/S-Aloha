import React from 'react'
import { describe, it, expect } from 'vitest'
import { render } from '@testing-library/react'
import Markdown, { blocks } from './Markdown'

describe('Markdown (KB-10)', () => {
  it('rend titres, listes, code et mise en forme', () => {
    const { container } = render(<Markdown source={'# Contexte\nUn **VPN** et `ping`.\n\n- un\n- deux\n\n1. premier\n2. second\n\n```\nipconfig /all\n```\n> Attention'} />)
    expect(container.querySelector('h2').textContent).toBe('Contexte')
    expect(container.querySelector('strong').textContent).toBe('VPN')
    expect(container.querySelector('p code').textContent).toBe('ping')
    expect(container.querySelectorAll('ul li')).toHaveLength(2)
    expect(container.querySelectorAll('ol li')).toHaveLength(2)
    expect(container.querySelector('pre code').textContent).toBe('ipconfig /all')
    expect(container.querySelector('blockquote').textContent).toBe('Attention')
  })

  it("n'interprète aucun HTML : une balise script reste du texte", () => {
    const { container } = render(<Markdown source={'<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>'} />)
    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('img')).toBeNull()
    expect(container.textContent).toContain('<script>alert(1)</script>')
  })

  it('ne garde que les liens sûrs', () => {
    const { container } = render(<Markdown source={'[doc](https://exemple.org) et [piège](javascript:alert(1))'} />)
    const links = container.querySelectorAll('a')
    expect(links).toHaveLength(1)
    expect(links[0].getAttribute('href')).toBe('https://exemple.org')
    expect(container.textContent).toContain('piège')
  })

  it('découpe un paragraphe sur plusieurs lignes en un seul bloc', () => {
    expect(blocks('ligne un\nligne deux\n\nautre')).toEqual([
      { type: 'p', text: 'ligne un ligne deux' }, { type: 'p', text: 'autre' }])
  })
})
