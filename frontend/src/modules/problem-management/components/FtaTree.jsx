import React from 'react'

let nextId = 1
const newNode = (label = '') => ({ id: 'n' + (nextId++) + '-' + Date.now(), label, gate: 'OR', children: [] })

/** Un nœud de l'arbre. Défini HORS de FtaTree : déclaré dans son rendu, il
 * était recréé à chaque frappe, React démontait le champ et la saisie
 * s'arrêtait au premier caractère (M1, revue du 2026-10-06). */
function FtaNode({ node, isRoot, onUpdate, onAdd, onRemove }) {
  return (
    <div className={'fta-node' + (node.children.length === 0 && !isRoot ? ' fta-base' : '')}>
      <div className="fta-label">
        {node.children.length > 0 &&
          <button className={'fta-gate ' + node.gate} title="Porte logique : cliquer pour basculer ET/OU"
            onClick={() => onUpdate(node, { gate: node.gate === 'OR' ? 'AND' : 'OR' })}>
            {node.gate === 'OR' ? 'OU' : 'ET'}
          </button>}
        <input style={{ maxWidth: 420 }} value={node.label}
          placeholder={isRoot ? 'Événement redouté (sommet de l\u0027arbre)' : 'Cause ou événement intermédiaire'}
          onChange={e => onUpdate(node, { label: e.target.value })} />
        <button className="btn ghost small" onClick={() => onAdd(node)}>+ cause</button>
        {!isRoot && <button className="btn ghost small" onClick={() => onRemove(node)}>×</button>}
      </div>
      {node.children.map(c =>
        <FtaNode key={c.id} node={c} onUpdate={onUpdate} onAdd={onAdd} onRemove={onRemove} />)}
    </div>
  )
}

/** data = { root: { label, gate, children[] }, rootCause } — arbre de défaillances (FTA). */
export default function FtaTree({ data, onChange }) {
  const root = data.root || newNode('Événement redouté')
  const set = (patch) => onChange({ ...data, root, ...patch })

  const update = (node, patch) => {
    const walk = n => n.id === node.id ? { ...n, ...patch } : { ...n, children: n.children.map(walk) }
    set({ root: walk(root) })
  }
  const addChild = (node) => update(node, { children: [...node.children, newNode()] })
  const removeNode = (target) => {
    const walk = n => ({ ...n, children: n.children.filter(c => c.id !== target.id).map(walk) })
    set({ root: walk(root) })
  }

  return (
    <div>
      <p style={{ color: 'var(--muted)', fontSize: 13, marginBottom: 14 }}>
        Arbre des défaillances (FTA) : partez de l'événement redouté et décomposez les causes.
        Les portes <b>OU/ET</b> indiquent si une seule cause suffit ou si elles doivent être combinées.
        Les feuilles (bord orange) sont les événements de base.
      </p>
      <FtaNode node={root} isRoot onUpdate={update} onAdd={addChild} onRemove={removeNode} />
      <div className="why-root">
        <b>Cause racine identifiée</b>
        <textarea rows={2} style={{ marginTop: 8 }} value={data.rootCause || ''}
          onChange={e => set({ rootCause: e.target.value })}
          placeholder="Le ou les événements de base retenus comme cause racine." />
      </div>
    </div>
  )
}
