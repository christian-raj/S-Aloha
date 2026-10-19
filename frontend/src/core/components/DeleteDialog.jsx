import React, { useEffect, useRef, useState } from 'react'

/**
 * SOC-14 (M8) : confirmation d'une suppression définitive. Rappelle ce qui est supprimé
 * (`reference`, `title`) et ce qui part avec (`impacts` : [{ label, count }]) ; le focus
 * va d'abord sur « Annuler », Échap ferme. `onConfirm` renvoie une promesse : son erreur
 * s'affiche dans la boîte, qui reste ouverte.
 */
export default function DeleteDialog({ title, reference, impacts = [], note, onConfirm, onClose }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const cancel = useRef(null)
  useEffect(() => {
    cancel.current?.focus()
    const onKey = e => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])
  const confirm = () => {
    setBusy(true)
    Promise.resolve(onConfirm()).catch(e => { setError(e.message); setBusy(false) })
  }
  return (
    <div className="modal-backdrop" onClick={e => e.target === e.currentTarget && !busy && onClose()}>
      <div className="modal" role="alertdialog" aria-modal="true" aria-labelledby="delete-title" aria-describedby="delete-body">
        <h2 id="delete-title">Supprimer {reference ? <b>{reference}</b> : 'cet élément'} ?</h2>
        <div id="delete-body">
          {title && <p className="modal-subject">{title}</p>}
          {impacts.length > 0 ? (
            <>
              <p>Partent aussi avec la suppression :</p>
              <ul className="plain-list modal-impacts">
                {impacts.map(i => <li key={i.label}><b>{i.count}</b> {i.label}</li>)}
              </ul>
            </>
          ) : <p>Rien d'autre n'est rattaché à cet élément.</p>}
          {note && <p className="muted small">{note}</p>}
          <p className="muted small">La suppression est définitive ; elle reste tracée au journal d'audit.</p>
        </div>
        {error && <div className="error" role="alert">{error}</div>}
        <div className="actions modal-actions">
          <button ref={cancel} className="btn ghost" onClick={onClose} disabled={busy}>Annuler</button>
          <button className="btn danger" onClick={confirm} disabled={busy}>{busy ? 'Suppression…' : 'Supprimer définitivement'}</button>
        </div>
      </div>
    </div>
  )
}
