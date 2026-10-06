import React, { useState } from 'react'

const DEFAULT_CATS = ['Main-d\u0027œuvre', 'Méthode', 'Machine', 'Matière', 'Milieu', 'Mesure']

/** data = { categories: { [nom]: string[] }, rootCause: string } */
export default function Ishikawa({ data, onChange }) {
  const cats = data.categories || Object.fromEntries(DEFAULT_CATS.map(c => [c, []]))
  const [drafts, setDrafts] = useState({})
  const set = (patch) => onChange({ ...data, categories: cats, ...patch })

  const addCause = (cat) => {
    const v = (drafts[cat] || '').trim()
    if (!v) return
    set({ categories: { ...cats, [cat]: [...cats[cat], v] } })
    setDrafts({ ...drafts, [cat]: '' })
  }
  const removeCause = (cat, i) =>
    set({ categories: { ...cats, [cat]: cats[cat].filter((_, idx) => idx !== i) } })

  return (
    <div>
      <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 14 }}>
        Diagramme d'Ishikawa (6M) : répartissez les causes possibles par famille, puis dégagez la cause racine.
      </p>
      <div className="fishbone">
        {Object.keys(cats).map(cat => (
          <div className="fish-cat" key={cat}>
            <h4>{cat}</h4>
            <ul>
              {cats[cat].map((c, i) => (
                <li key={i}>{c}<button onClick={() => removeCause(cat, i)} title="Retirer">×</button></li>
              ))}
            </ul>
            <div style={{ display: 'flex', gap: 6 }}>
              <input value={drafts[cat] || ''} placeholder="Ajouter une cause…"
                onChange={e => setDrafts({ ...drafts, [cat]: e.target.value })}
                onKeyDown={e => e.key === 'Enter' && addCause(cat)} />
              <button className="btn small" onClick={() => addCause(cat)}>+</button>
            </div>
          </div>
        ))}
      </div>
      <div className="why-root">
        <b>Cause racine identifiée</b>
        <textarea rows={2} style={{ marginTop: 8 }} value={data.rootCause || ''}
          onChange={e => set({ rootCause: e.target.value })}
          placeholder="Synthèse : la ou les causes fondamentales retenues." />
      </div>
    </div>
  )
}
