import React, { useState } from 'react'

// Graphiques du tableau de bord (guide de visualisation) : traits de 2 px,
// grille fine, légende dès deux séries, étiquettes directes en bout de courbe,
// réticule au survol et vue tableau. Couleurs : --chart-1 à --chart-3,
// palette catégorielle validée en clair et en sombre (styles.css).

const day = (iso) => new Date(iso).toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit' })

/** Graduations rondes (0, 2, 5, 10…), prolongées jusqu'à la graduation qui couvre le maximum. */
export function ticks(max) {
  const step = max <= 10 ? 2 : max <= 25 ? 5 : max <= 50 ? 10 : Math.ceil(max / 5 / 10) * 10
  const n = Math.ceil(Math.max(max, 1) / step)
  return Array.from({ length: n + 1 }, (_, i) => i * step)
}

/**
 * Courbes journalières. `series` : [{ key, label, slot }] — `slot` 1 à 3 choisit
 * la couleur ; chaque série porte aussi son étiquette en bout de courbe : la
 * couleur n'est jamais le seul repère.
 */
export function LineChart({ title, days, series, unit = '' }) {
  const [hover, setHover] = useState(null)
  const [table, setTable] = useState(false)
  const W = 640, H = 240, M = { top: 16, right: 92, bottom: 28, left: 36 }
  if (!days?.length) return <ChartCard title={title}><p className="muted small">Aucune donnée sur la période.</p></ChartCard>

  const yTicks = ticks(Math.max(...days.flatMap(d => series.map(s => d[s.key]))))
  const top = yTicks[yTicks.length - 1]
  const x = (i) => M.left + (days.length === 1 ? 0 : (i * (W - M.left - M.right)) / (days.length - 1))
  const y = (v) => M.top + (1 - v / top) * (H - M.top - M.bottom)
  const path = (key) => days.map((d, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(d[key]).toFixed(1)}`).join('')
  const labelEvery = Math.ceil(days.length / 8)
  const last = days.length - 1
  // Étiquettes de fin écartées d'au moins 13 px pour rester lisibles quand les courbes se rejoignent.
  const ends = series.map(s => ({ s, py: y(days[last][s.key]) })).sort((a, b) => a.py - b.py)
  ends.forEach((e, i) => { if (i && e.py - ends[i - 1].py < 13) e.py = ends[i - 1].py + 13 })

  const onMove = (e) => {
    const r = e.currentTarget.getBoundingClientRect()
    const px = ((e.clientX - r.left) / r.width) * W
    const i = Math.round(((px - M.left) / (W - M.left - M.right)) * last)
    setHover(Math.max(0, Math.min(last, i)))
  }
  const h = hover === null ? null : days[hover]

  return (
    <ChartCard title={title} onToggle={() => setTable(v => !v)} table={table}
      legend={series.map(s => <span key={s.key}><i className={`key c${s.slot}`} />{s.label}</span>)}>
      {table ? (
        <div className="table-scroll">
          <table className="compact">
            <thead><tr><th>Jour</th>{series.map(s => <th key={s.key}>{s.label}</th>)}</tr></thead>
            <tbody>{days.map(d => <tr key={d.date}><td>{day(d.date)}</td>{series.map(s => <td key={s.key}>{d[s.key]}</td>)}</tr>)}</tbody>
          </table>
        </div>
      ) : (
        <div className="chart-wrap">
          <svg viewBox={`0 0 ${W} ${H}`} role="img" onMouseMove={onMove} onMouseLeave={() => setHover(null)}
            aria-label={`${title} : ${series.map(s => `${s.label} ${days[last][s.key]}${unit}`).join(', ')}`}>
            {yTicks.map(t => (
              <g key={t}>
                <line className="gridline" x1={M.left} x2={W - M.right} y1={y(t)} y2={y(t)} />
                <text className="tick" x={M.left - 6} y={y(t) + 4} textAnchor="end">{t}</text>
              </g>))}
            {days.map((d, i) => i % labelEvery === 0 &&
              <text key={d.date} className="tick" x={x(i)} y={H - 8} textAnchor="middle">{day(d.date)}</text>)}
            {series.map(s => <path key={s.key} className={`line c${s.slot}`} d={path(s.key)} />)}
            {ends.map(({ s, py }) => (
              <g key={s.key}>
                <circle className={`end-dot c${s.slot}`} cx={x(last)} cy={y(days[last][s.key])} r="4" />
                <text className="end-label" x={x(last) + 9} y={py + 4}>{days[last][s.key]}{unit} {s.short ?? ''}</text>
              </g>))}
            {h && <line className="crosshair" x1={x(hover)} x2={x(hover)} y1={M.top} y2={H - M.bottom} />}
            <rect x={M.left} y={M.top} width={W - M.left - M.right} height={H - M.top - M.bottom} fill="transparent" />
          </svg>
          {h && (
            <div className="chart-tip" style={{ left: `${(x(hover) / W) * 100}%` }} role="status">
              <b>{day(h.date)}</b>
              {series.map(s => <div key={s.key}><i className={`key c${s.slot}`} />{s.label} : {h[s.key]}{unit}</div>)}
            </div>
          )}
        </div>
      )}
    </ChartCard>
  )
}

/** Cadre commun : titre, légende, bascule graphique / données. */
export function ChartCard({ title, legend, onToggle, table, children, note }) {
  return (
    <figure className="card chart-card">
      <figcaption>
        <h3>{title}</h3>
        {onToggle && <button className="btn ghost small no-print" onClick={onToggle}>{table ? 'Voir le graphique' : 'Voir les données'}</button>}
      </figcaption>
      {legend && <div className="chart-legend">{legend}</div>}
      {children}
      {note && <p className="muted small chart-note">{note}</p>}
    </figure>
  )
}

/**
 * Barres horizontales étiquetées (valeur écrite au bout de chaque barre) :
 * répartitions, entonnoir. `data` : [{ label, count, hint? }].
 */
export function HBars({ title, data, empty = 'Aucune donnée sur la période.', unit = '', max, note, tone }) {
  const top = max ?? Math.max(1, ...data.map(d => d.count))
  return (
    <ChartCard title={title} note={note}>
      {data.length === 0 && <p className="muted small">{empty}</p>}
      <div className="hbars">
        {data.map(d => (
          <div className="bar-row" key={d.label}>
            <span className="lbl" title={d.label}>{d.label}</span>
            <div className="bar-track"><div className={`bar ${tone ?? ''}`} style={{ width: `${(d.count / top) * 100}%` }} /></div>
            <span className="val">{d.count}{unit}{d.hint && <small className="muted"> {d.hint}</small>}</span>
          </div>
        ))}
      </div>
    </ChartCard>
  )
}

/** Chiffre clé ; `alert` : bord et chiffre rouges, et la mention écrite de l'alerte. */
export function Kpi({ value, label, sub, alert, suffix = '' }) {
  return (
    <div className={`kpi${alert ? ' alert' : ''}`}>
      <div className="n">{value ?? '—'}{value != null && suffix}</div>
      <div className="l">{label}</div>
      {alert && <div className="kpi-alert">{alert}</div>}
      {sub && <div className="kpi-sub">{sub}</div>}
    </div>
  )
}

const TOLERANCE_TONE = { 'Dans la cible': 't-done', 'À surveiller': 't-wait', 'Hors tolérance': 't-bad', 'Sans objet': 't-closed' }
/** État d'une tolérance de projet (PRJ-31/32). */
export const Tolerance = ({ value }) => <span className={`badge ${TOLERANCE_TONE[value] ?? 't-closed'}`}>{value}</span>
