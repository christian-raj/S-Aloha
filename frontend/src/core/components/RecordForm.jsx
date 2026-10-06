import React from 'react'
import DirectoryPicker from './DirectoryPicker'

/*
 * Formulaire décrit par une liste de champs (configuration des modules) :
 *   { key, label, type, options, required, default, full, create, readOnly, placeholder, rows }
 * type : text (défaut), textarea, select, date, datetime, number, checkbox,
 *        person (utilisateur/groupe AD : clés `key` et `nameKey`, `typeKey` facultatif).
 * `create: false` masque le champ dans le formulaire de création ; `numeric`
 * envoie la valeur d'une liste comme nombre (identifiant, étape…).
 */

const pad = n => String(n).padStart(2, '0')
/** ISO UTC → valeur d'un <input type="datetime-local"> (heure locale). */
const toLocalInput = iso => {
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** Valeurs de formulaire à partir d'un enregistrement de l'API (ou de valeurs par défaut). */
export function toForm(fields, record = {}) {
  const f = {}
  for (const fd of fields) {
    const v = record[fd.key] ?? fd.default
    if (fd.type === 'date') f[fd.key] = v ? String(v).slice(0, 10) : ''
    else if (fd.type === 'datetime') f[fd.key] = v ? toLocalInput(v) : ''
    else if (fd.type === 'checkbox') f[fd.key] = !!v
    else f[fd.key] = v ?? ''
    if (fd.type === 'person') {
      f[fd.nameKey] = record[fd.nameKey] ?? ''
      if (fd.typeKey) f[fd.typeKey] = record[fd.typeKey] ?? ''
    }
  }
  return f
}

/** Corps de requête à partir des valeurs de formulaire. */
export function toPayload(fields, form) {
  const p = {}
  for (const fd of fields) {
    const v = form[fd.key]
    if (fd.type === 'date' || fd.type === 'datetime') p[fd.key] = v ? new Date(v).toISOString() : null
    else if (fd.type === 'number') p[fd.key] = v === '' || v === null ? null : Number(v)
    else if (fd.type === 'select') p[fd.key] = v === '' ? null : fd.numeric ? Number(v) : v
    else p[fd.key] = v
    if (fd.type === 'person') {
      p[fd.nameKey] = form[fd.nameKey] || null
      if (fd.typeKey) p[fd.typeKey] = form[fd.typeKey] || null
      p[fd.key] = v || null
    }
  }
  return p
}

function PersonInput({ fd, form, onChange, disabled }) {
  const id = form[fd.key]
  const set = (entry) => onChange({
    [fd.key]: entry?.id ?? '', [fd.nameKey]: entry?.displayName ?? '',
    ...(fd.typeKey ? { [fd.typeKey]: entry?.type ?? '' } : {})
  })
  return (
    <>
      {id && (
        <span className="chip">
          {form[fd.typeKey] === 'Group' ? '👥' : '👤'} {form[fd.nameKey] || id}
          {!disabled && <button type="button" onClick={() => set(null)} title="Retirer">×</button>}
        </span>
      )}
      {!disabled && <DirectoryPicker onPick={set} placeholder={id ? 'Remplacer…' : 'Rechercher dans l\'annuaire…'} />}
    </>
  )
}

function Field({ fd, form, onChange, disabled }) {
  const v = form[fd.key]
  const set = e => onChange({ [fd.key]: fd.type === 'checkbox' ? e.target.checked : e.target.value })
  let input
  switch (fd.type) {
    case 'textarea':
      input = <textarea rows={fd.rows ?? 4} value={v} onChange={set} disabled={disabled} placeholder={fd.placeholder} />
      break
    case 'select':
      input = (
        <select value={v} onChange={set} disabled={disabled}>
          {!fd.required && <option value="">—</option>}
          {fd.options.map(o => typeof o === 'object'
            ? <option key={o.value} value={o.value}>{o.label}</option>
            : <option key={o}>{o}</option>)}
        </select>)
      break
    case 'date': input = <input type="date" value={v} onChange={set} disabled={disabled} />; break
    case 'datetime': input = <input type="datetime-local" value={v} onChange={set} disabled={disabled} />; break
    case 'number': input = <input type="number" step="any" value={v} onChange={set} disabled={disabled} />; break
    case 'checkbox':
      input = (
        <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontWeight: 400 }}>
          <input type="checkbox" checked={v} onChange={set} disabled={disabled} style={{ width: 'auto' }} /> {fd.checkboxLabel}
        </label>)
      break
    case 'person': input = <PersonInput fd={fd} form={form} onChange={onChange} disabled={disabled} />; break
    default: input = <input value={v} onChange={set} disabled={disabled} placeholder={fd.placeholder} />
  }
  return (
    <div className={'field' + (fd.full || fd.type === 'textarea' ? ' full' : '')}>
      <label>{fd.label}{fd.required && ' *'}</label>
      {input}
    </div>
  )
}

/** Grille de champs ; `onChange` reçoit un objet partiel à fusionner dans `form`. */
export default function RecordForm({ fields, form, onChange, disabled, creating }) {
  return (
    <div className="form-grid">
      {fields.filter(fd => !(creating && fd.create === false)).map(fd =>
        <Field key={fd.key} fd={fd} form={form} onChange={onChange} disabled={disabled || fd.readOnly} />)}
    </div>
  )
}
