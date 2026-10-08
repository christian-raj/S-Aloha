import React, { useEffect, useState } from 'react'
import { api, getUser } from '../../../api'
import { dateTimeFr } from '../../../core/fields'

const ANSSI_URL = 'https://cyber.gouv.fr/'

/**
 * Paramétrage → Référentiel NIS 2 (Admin) : la structure du ReCyF est fournie
 * avec S-Aloha ; le texte des exigences, dont la réutilisation commerciale
 * est soumise à l'autorisation de l'ANSSI, est importé ici (NIS-21, ADR-0012).
 */
export default function ReferentialSettings() {
  const [ref, setRef] = useState(null)
  const [error, setError] = useState('')
  const [report, setReport] = useState(null)
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(null)
  const isAdmin = getUser()?.role === 'Admin'
  const load = () => api.compliance.referential().then(setRef).catch(e => setError(e.message))
  useEffect(() => { load() }, [])

  const importFile = async (file) => {
    if (!file) return
    setBusy(true); setReport(null); setError('')
    try {
      setReport(await api.compliance.importText(await file.text()))
      await load()
    } catch (e) { setError(e.message) } finally { setBusy(false) }
  }

  if (!ref) return error ? <div className="error">{error}</div> : <p>Chargement…</p>
  return (
    <>
      <h1 className="page-title">Référentiel NIS 2</h1>
      <p className="page-sub">{ref.version} — {ref.objectives.length} objectifs de sécurité, {ref.requirements} exigences.</p>
      {error && <div className="error">{error}</div>}

      <div className="card" style={{ marginBottom: 16 }}>
        <h3 style={{ marginBottom: 6 }}>Texte des exigences</h3>
        <p style={{ marginBottom: 10 }}>
          <b>{ref.withText}</b> exigence(s) sur {ref.requirements} ont leur texte
          {ref.lastImport && <> · dernier import le {dateTimeFr(ref.lastImport)}</>}.
        </p>
        <p className="muted small" style={{ marginBottom: 12 }}>
          S-Aloha fournit la structure du référentiel (objectifs, identifiants, cibles, et n° des mesures ISO 27002
          correspondantes selon la table de correspondance de l'ANSSI, sans leurs intitulés). Le texte des exigences est publié par l'<a href={ANSSI_URL} target="_blank" rel="noreferrer">ANSSI</a>,
          dont la réutilisation commerciale est soumise à autorisation : importez-le depuis un fichier CSV (séparateur « ; »
          ou « , ») comportant une colonne « Référence » (ou « Code ») et une colonne « Contenu » (ou « Texte »).
          Un import ultérieur met à jour les textes modifiés sans toucher aux réponses des évaluations.
        </p>
        {isAdmin
          ? <label className="btn ghost" style={{ display: 'inline-flex', cursor: busy ? 'wait' : 'pointer' }}>
              {busy ? 'Import en cours…' : 'Importer un fichier CSV'}
              <input type="file" accept=".csv,text/csv" hidden disabled={busy}
                onChange={e => { importFile(e.target.files[0]); e.target.value = '' }} />
            </label>
          : <p className="muted small">L'import est réservé aux administrateurs.</p>}
        {report && (
          <div className="banner ok card" style={{ marginTop: 12, marginBottom: 0, display: 'block' }}>
            <b>Import terminé</b> : {report.updated} texte(s) mis à jour, {report.unchanged} inchangé(s),
            {' '}{report.withoutText} exigence(s) encore sans texte.
            {report.unknown.length > 0 && <div className="small" style={{ marginTop: 4 }}>
              Identifiants inconnus ignorés ({report.unknown.length}) : {report.unknown.join(', ')}</div>}
          </div>
        )}
      </div>

      {ref.objectives.map(o => {
        const isOpen = open === o.id
        return (
          <div key={o.id} className="card" style={{ marginBottom: 8, padding: 0 }}>
            <button className="obj-toggle" style={{ padding: '12px 16px' }} aria-expanded={isOpen} onClick={() => setOpen(isOpen ? null : o.id)}>
              <span><b>{o.id}. {o.title}</b> <span className="muted small">· {o.pillar}</span></span>
              <span className="muted small">{o.requirements.filter(r => r.text).length}/{o.requirements.length} avec texte</span>
            </button>
            {isOpen && (
              <table>
                <thead><tr><th>Exigence</th><th>Thématique</th><th>Cibles</th><th>ISO 27002</th><th>Texte</th></tr></thead>
                <tbody>{o.requirements.map(r => (
                  <tr key={r.id}>
                    <td style={{ whiteSpace: 'nowrap' }}><b>{r.code}</b></td>
                    <td>{r.theme}</td>
                    <td>{[r.forImportant && 'EI', r.forEssential && 'EE'].filter(Boolean).join(', ')}</td>
                    <td className="small">{r.isoControls.split(' ').filter(Boolean).map(c => c.replace('27002:2022-', '')).join(', ') || '—'}</td>
                    <td className="small" style={{ whiteSpace: 'pre-line' }}>{r.text || <span className="muted">non importé</span>}</td>
                  </tr>))}</tbody>
              </table>
            )}
          </div>
        )
      })}
    </>
  )
}
