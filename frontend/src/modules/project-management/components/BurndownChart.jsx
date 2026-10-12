import React, { useState } from 'react'

const W = 640, H = 240, M = { top: 16, right: 56, bottom: 28, left: 36 }
const day = (iso) => new Date(iso).toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit' })
/** Graduations rondes de l'axe des points (0, 5, 10…). */
const ticks = (max) => {
  const step = max <= 10 ? 2 : max <= 25 ? 5 : max <= 50 ? 10 : Math.ceil(max / 5 / 10) * 10
  return Array.from({ length: Math.floor(max / step) + 1 }, (_, i) => i * step)
}

/**
 * Burndown d'un sprint (PRJ-13) : points restants (série) et ligne idéale
 * (référence en pointillé, identifiée aussi par son étiquette, pas par la seule
 * couleur). Réticule au survol, vue tableau, couleurs validées en clair et en
 * sombre (--chart-series, --chart-reference).
 */
export default function BurndownChart({ data, sprintName }) {
  const [hover, setHover] = useState(null)
  const [table, setTable] = useState(false)
  const days = data.days
  if (days.length < 2) return <p className="muted small">Sprint d'un seul jour : pas de courbe à tracer.</p>

  const maxY = Math.max(data.total, 1)
  let yTicks = ticks(maxY)
  // L'axe monte jusqu'à la graduation ronde suivante : le haut de la courbe garde un repère.
  if (yTicks[yTicks.length - 1] < maxY) yTicks = [...yTicks, yTicks[yTicks.length - 1] + (yTicks[1] - yTicks[0])]
  const top = yTicks[yTicks.length - 1]
  const x = (i) => M.left + (i * (W - M.left - M.right)) / (days.length - 1)
  const y = (v) => M.top + (1 - v / top) * (H - M.top - M.bottom)
  const path = (key) => days.map((d, i) => (d[key] === null ? null : [x(i), y(d[key])]))
    .filter(Boolean).map(([px, py], i) => `${i ? 'L' : 'M'}${px.toFixed(1)},${py.toFixed(1)}`).join('')
  const lastReal = days.reduce((acc, d, i) => (d.remaining === null ? acc : i), -1)
  const labelEvery = Math.ceil(days.length / 8)

  const onMove = (e) => {
    const r = e.currentTarget.getBoundingClientRect()
    const px = ((e.clientX - r.left) / r.width) * W
    const i = Math.round(((px - M.left) / (W - M.left - M.right)) * (days.length - 1))
    setHover(Math.max(0, Math.min(days.length - 1, i)))
  }
  const h = hover === null ? null : days[hover]

  return (
    <figure className="burndown" aria-label={`Burndown du sprint ${sprintName}`}>
      <div className="chart-legend" aria-hidden>
        <span><i className="key series" />Points restants</span>
        <span><i className="key reference" />Idéal</span>
        <button className="btn ghost small" style={{ marginLeft: 'auto' }} onClick={() => setTable(v => !v)}>
          {table ? 'Voir le graphique' : 'Voir les données'}</button>
      </div>
      {table ? (
        <table>
          <thead><tr><th>Jour</th><th>Idéal</th><th>Points restants</th></tr></thead>
          <tbody>{days.map(d => <tr key={d.date}><td>{day(d.date)}</td><td>{d.ideal}</td><td>{d.remaining ?? '—'}</td></tr>)}</tbody>
        </table>
      ) : (
        <div className="chart-wrap">
          <svg viewBox={`0 0 ${W} ${H}`} role="img" onMouseMove={onMove} onMouseLeave={() => setHover(null)}
            aria-label={`Points restants : ${days[lastReal]?.remaining ?? data.total} sur ${data.total}`}>
            {yTicks.map(t => (
              <g key={t}>
                <line className="grid" x1={M.left} x2={W - M.right} y1={y(t)} y2={y(t)} />
                <text className="tick" x={M.left - 6} y={y(t) + 4} textAnchor="end">{t}</text>
              </g>))}
            {days.map((d, i) => i % labelEvery === 0 &&
              <text key={d.date} className="tick" x={x(i)} y={H - 8} textAnchor="middle">{day(d.date)}</text>)}
            <path className="line reference" d={path('ideal')} />
            <path className="line series" d={path('remaining')} />
            {lastReal >= 0 && <>
              <circle className="end-dot" cx={x(lastReal)} cy={y(days[lastReal].remaining)} r="4.5" />
              <text className="end-label" x={x(lastReal) + 9} y={y(days[lastReal].remaining) + 4}>{days[lastReal].remaining} pts</text>
            </>}
            {h && <line className="crosshair" x1={x(hover)} x2={x(hover)} y1={M.top} y2={H - M.bottom} />}
            <rect x={M.left} y={M.top} width={W - M.left - M.right} height={H - M.top - M.bottom} fill="transparent" />
          </svg>
          {h && (
            <div className="chart-tip" style={{ left: `${(x(hover) / W) * 100}%` }} role="status">
              <b>{day(h.date)}</b>
              <div><i className="key series" />Restant : {h.remaining ?? '—'}</div>
              <div><i className="key reference" />Idéal : {h.ideal}</div>
            </div>
          )}
        </div>
      )}
    </figure>
  )
}
