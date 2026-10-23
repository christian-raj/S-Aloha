import React, { useState } from 'react'
import { api, getUser } from '../../../api'
import RecordList from '../../../core/components/RecordList'
import { ciConfig } from '../config'

/**
 * CFG-30 : import en masse (Admin). Contrôle d'abord, puis import : une seule ligne refusée et
 * rien n'est créé ; le rapport donne la ligne et le motif.
 */
export function CiImport({ onDone }) {
  const [csv, setCsv] = useState('')
  const [result, setResult] = useState(null)
  const [busy, setBusy] = useState(false)
  const run = dryRun => {
    setBusy(true)
    api.configurationItems.importCsv(csv, dryRun)
      .then(r => { setResult({ ok: true, ...r }); if (!dryRun) { setCsv(''); onDone?.() } })
      .catch(e => setResult({ ok: false, message: e.message, errors: e.body?.errors ?? [] }))
      .finally(() => setBusy(false))
  }
  const file = e => e.target.files?.[0]?.text().then(t => { setCsv(t); setResult(null) })
  return (
    <details className="card" style={{ marginBottom: 16 }}>
      <summary><b>Importer des CI (CSV)</b></summary>
      <p className="muted small">En-tête : <code>nom;type;environnement;emplacement;propriétaire;statut</code> (nom et type obligatoires ; statut Planifié, environnement Production par défaut). Séparateur « ; » ou « , ».</p>
      <input type="file" accept=".csv,text/csv" aria-label="Fichier CSV" onChange={file} />
      <textarea rows={6} aria-label="Contenu CSV" value={csv} onChange={e => { setCsv(e.target.value); setResult(null) }} style={{ marginTop: 8 }} />
      <button className="btn ghost" onClick={() => run(true)} disabled={busy || !csv.trim()}>Contrôler</button>{' '}
      <button className="btn" onClick={() => run(false)} disabled={busy || !csv.trim()}>Importer</button>
      {result && <div className={result.ok ? 'notice ok' : 'error'} role="status" style={{ marginTop: 12 }}>{result.message}
        {result.errors?.length > 0 && <ul className="small">{result.errors.map(e => <li key={e.line}>Ligne {e.line}{e.name && ` (${e.name})`} : {e.message}</li>)}</ul>}
      </div>}
    </details>
  )
}

export default function ConfigurationItems() {
  const [key, setKey] = useState(0)
  return (
    <>
      {getUser()?.role === 'Admin' && <CiImport onDone={() => setKey(k => k + 1)} />}
      <RecordList key={key} config={ciConfig} />
    </>
  )
}
