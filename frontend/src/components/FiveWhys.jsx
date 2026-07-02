import React from 'react'

/** data = { problem: string, whys: string[], rootCause: string } */
export default function FiveWhys({ data, onChange }) {
  const whys = data.whys || ['']
  const set = (patch) => onChange({ ...data, ...patch })
  const setWhy = (i, v) => set({ whys: whys.map((w, idx) => idx === i ? v : w) })

  return (
    <div>
      <div className="field">
        <label>Énoncé du problème</label>
        <input value={data.problem || ''} onChange={e => set({ problem: e.target.value })}
          placeholder="Que s'est-il passé, concrètement ?" />
      </div>
      <div className="why-chain">
        {whys.map((w, i) => (
          <React.Fragment key={i}>
            {i > 0 && <div className="why-connector"><div /></div>}
            <div className="why-step">
              <div className="why-num">{i + 1}</div>
              <div className="why-body">
                <label>Pourquoi ?</label>
                <input value={w} onChange={e => setWhy(i, e.target.value)}
                  placeholder={'Cause de niveau ' + (i + 1)} />
              </div>
            </div>
          </React.Fragment>
        ))}
      </div>
      <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        {whys.length < 7 && <button className="btn ghost small" onClick={() => set({ whys: [...whys, ''] })}>+ Ajouter un pourquoi</button>}
        {whys.length > 1 && <button className="btn ghost small" onClick={() => set({ whys: whys.slice(0, -1) })}>− Retirer le dernier</button>}
      </div>
      <div className="why-root">
        <b>Cause racine identifiée</b>
        <textarea rows={2} style={{ marginTop: 8 }} value={data.rootCause || ''}
          onChange={e => set({ rootCause: e.target.value })}
          placeholder="La cause fondamentale, une fois la chaîne des pourquoi épuisée." />
      </div>
    </div>
  )
}
