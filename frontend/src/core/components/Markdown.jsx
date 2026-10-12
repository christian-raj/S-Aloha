import React from 'react'

/*
 * Rendu Markdown sûr (KB-10, issue #25) : titres, paragraphes, listes à puces et
 * numérotées, citations, blocs de code, et en ligne : `code`, **gras**, *italique*,
 * [liens](https://…). Le texte devient des éléments React, jamais du HTML :
 * une balise <script> saisie s'affiche telle quelle. Les liens n'acceptent que
 * http(s), mailto, et les chemins de l'application.
 */

const SAFE_URL = /^(https?:\/\/|mailto:|\/|#)/i
// Ordre : code, lien, gras, italique (un seul niveau d'imbrication : gras et italique dans un lien).
const INLINE = /(`[^`]+`)|(\[[^\]]+\]\([^)\s]+\))|(\*\*[^*]+\*\*)|(\*[^*\s][^*]*\*|_[^_\s][^_]*_)/g

export function inline(text, key = 'i') {
  const out = []
  let last = 0, n = 0
  for (const m of text.matchAll(INLINE)) {
    if (m.index > last) out.push(text.slice(last, m.index))
    const t = m[0], k = `${key}-${n++}`
    if (m[1]) out.push(<code key={k}>{t.slice(1, -1)}</code>)
    else if (m[2]) {
      const [, label, url] = t.match(/^\[([^\]]+)\]\(([^)\s]+)\)$/)
      out.push(SAFE_URL.test(url)
        ? <a key={k} href={url} target={/^https?:/i.test(url) ? '_blank' : undefined} rel="noopener noreferrer">{inline(label, k)}</a>
        : label)
    }
    else if (m[3]) out.push(<strong key={k}>{inline(t.slice(2, -2), k)}</strong>)
    else out.push(<em key={k}>{t.slice(1, -1)}</em>)
    last = m.index + t.length
  }
  if (last < text.length) out.push(text.slice(last))
  return out
}

/** Découpe le texte en blocs : { type, … }. */
export function blocks(source) {
  const lines = (source ?? '').replace(/\r\n?/g, '\n').split('\n')
  const out = []
  let i = 0
  const isList = (l, ordered) => ordered ? /^\s*\d+[.)]\s+/.test(l) : /^\s*[-*+]\s+/.test(l)
  while (i < lines.length) {
    const line = lines[i]
    if (!line.trim()) { i++; continue }
    const fence = line.match(/^\s*```/)
    if (fence) {
      const code = []
      i++
      while (i < lines.length && !/^\s*```/.test(lines[i])) code.push(lines[i++])
      i++   // clôture (absente : jusqu'à la fin)
      out.push({ type: 'code', text: code.join('\n') })
      continue
    }
    const heading = line.match(/^(#{1,6})\s+(.*)$/)
    if (heading) { out.push({ type: 'heading', level: heading[1].length, text: heading[2].trim() }); i++; continue }
    if (/^\s*>/.test(line)) {
      const quote = []
      while (i < lines.length && /^\s*>/.test(lines[i])) quote.push(lines[i++].replace(/^\s*>\s?/, ''))
      out.push({ type: 'quote', text: quote.join(' ') })
      continue
    }
    for (const ordered of [false, true]) {
      if (!isList(line, ordered)) continue
      const items = []
      while (i < lines.length && isList(lines[i], ordered))
        items.push(lines[i++].replace(ordered ? /^\s*\d+[.)]\s+/ : /^\s*[-*+]\s+/, ''))
      out.push({ type: ordered ? 'ol' : 'ul', items })
      break
    }
    if (lines[i] !== line) continue
    const para = []
    while (i < lines.length && lines[i].trim() && !/^(#{1,6})\s|^\s*```|^\s*>/.test(lines[i])
           && !isList(lines[i], false) && !isList(lines[i], true)) para.push(lines[i++].trim())
    out.push({ type: 'p', text: para.join(' ') })
  }
  return out
}

/** Contenu Markdown rendu ; les titres commencent au niveau h2 (le h1 est le titre de la fiche). */
export default function Markdown({ source, empty = null }) {
  const list = blocks(source)
  if (list.length === 0) return empty
  return (
    <div className="markdown">
      {list.map((b, n) => {
        const k = 'b' + n
        switch (b.type) {
          case 'heading': return React.createElement('h' + Math.min(b.level + 1, 6), { key: k }, inline(b.text, k))
          case 'code': return <pre key={k}><code>{b.text}</code></pre>
          case 'quote': return <blockquote key={k}>{inline(b.text, k)}</blockquote>
          case 'ul': return <ul key={k}>{b.items.map((t, j) => <li key={j}>{inline(t, `${k}-${j}`)}</li>)}</ul>
          case 'ol': return <ol key={k}>{b.items.map((t, j) => <li key={j}>{inline(t, `${k}-${j}`)}</li>)}</ol>
          default: return <p key={k}>{inline(b.text, k)}</p>
        }
      })}
    </div>
  )
}
