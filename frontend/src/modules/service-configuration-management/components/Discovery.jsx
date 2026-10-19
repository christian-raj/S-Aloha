import React from 'react'
import { RULE_LABELS, SOURCE_LABELS, STAGING_TONES } from '../config'

/**
 * Score de rapprochement (CFG-41) : jauge et valeur écrite ; la couleur n'est jamais le seul
 * repère (le critère est nommé à côté). Seuil du rapprochement automatique : 80.
 */
export function ScoreMeter({ score, rule }) {
  const tone = score >= 80 ? 'ok' : score >= 50 ? 'warn' : 'none'
  return (
    <span className="score-meter" title={`Score ${score} / 100 — ${RULE_LABELS[rule] ?? rule}`}>
      <span className="bar-track"><span className={'bar ' + tone} style={{ width: `${score}%` }} /></span>
      <b>{score}</b>{rule && rule !== 'None' && <small className="muted">{RULE_LABELS[rule] ?? rule}</small>}
    </span>
  )
}

export const StagingBadge = ({ status }) => <span className={'badge ' + (STAGING_TONES[status] ?? 't-closed')}>{status}</span>

/** Sources qui ont vu l'actif, en clair. */
export const Sources = ({ value }) => (value || '').split(',').filter(Boolean).map(s => SOURCE_LABELS[s] ?? s).join(', ') || '—'

/** Liste « libellé : valeur » compacte ; une valeur vide s'affiche « — ». */
export function KeyValues({ rows }) {
  return (
    <dl className="kv">
      {rows.map(([label, value]) => (
        <React.Fragment key={label}>
          <dt>{label}</dt><dd>{value === null || value === undefined || value === '' ? '—' : value}</dd>
        </React.Fragment>
      ))}
    </dl>
  )
}
