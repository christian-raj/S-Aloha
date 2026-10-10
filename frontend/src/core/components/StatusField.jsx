import React from 'react'
import { reachable } from '../useTransitions'

/**
 * Choix du statut (SOC-05) : statut courant et successeurs permis. Un Admin voit
 * aussi les autres statuts, marqués « transition forcée » : les choisir exige un
 * motif, tracé au journal d'audit (SOC-20).
 * `statuses` : [{ value, manager? }] ; `reason`/`onReason` : motif d'un forçage.
 */
export default function StatusField({ statuses, current, value, onChange, graph, isManager, isAdmin, disabled, reason, onReason }) {
  const forced = s => !reachable(graph, current, s)
  const visible = statuses.filter(s => !forced(s.value) || isAdmin)
  return (
    <>
      <div className="field" style={{ maxWidth: 300 }}>
        <label>Statut</label>
        <select value={value} onChange={e => onChange(e.target.value)} disabled={disabled}>
          {visible.map(s => (
            <option key={s.value} value={s.value} disabled={s.manager && !isManager && s.value !== current}>
              {s.value}{forced(s.value) ? ' (transition forcée)' : s.manager ? ' (gestionnaire)' : ''}
            </option>))}
        </select>
      </div>
      {forced(value) && (
        <div className="field">
          <label>Motif de la transition forcée *</label>
          <input value={reason} onChange={e => onReason(e.target.value)}
            placeholder="Obligatoire : tracé au journal d'audit" />
        </div>
      )}
    </>
  )
}

/** Options d'enregistrement : forçage si le statut choisi sort du graphe. */
export const forceOptions = (graph, current, status, reason) =>
  reachable(graph, current, status) ? undefined : { force: true, reason }
