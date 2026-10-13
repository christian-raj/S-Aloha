import React, { useEffect, useState } from 'react'
import { api, getUser } from '../../api'
import { dateTimeFr } from '../fields'

/**
 * Fil de commentaires d'un enregistrement (SOC-24). Une note de travail ne se lit et ne
 * s'écrit que par ceux qui traitent l'enregistrement (l'API en décide : `canWriteNotes`) ;
 * elle est marquée et ne se montre jamais au demandeur.
 */
export default function Comments({ type, id }) {
  const [data, setData] = useState(null)
  const [body, setBody] = useState('')
  const [note, setNote] = useState(false)
  const [error, setError] = useState('')
  const me = (getUser()?.username ?? '').toLowerCase()
  const isAdmin = getUser()?.role === 'Admin'
  const load = () => api.comments.list(type, id).then(setData).catch(e => setError(e.message))
  useEffect(() => { load() }, [type, id])

  const send = () => api.comments.create({ type, id, visibility: note ? 'Note' : 'Public', body })
    .then(() => { setBody(''); setError(''); load() }).catch(e => setError(e.message))
  const remove = (c) => window.confirm('Retirer ce commentaire ?')
    && api.comments.remove(c.id).then(load).catch(e => setError(e.message))

  if (!data) return error ? <div className="error">{error}</div> : <p>Chargement…</p>
  return (
    <div className="card">
      <h3 style={{ color: 'var(--navy)', marginBottom: 10 }}>Commentaires ({data.comments.length})</h3>
      {error && <div className="error">{error}</div>}
      {data.comments.length === 0 && <p className="muted" style={{ marginBottom: 12 }}>Aucun commentaire pour l'instant.</p>}
      <ul className="comments">
        {data.comments.map(c => (
          <li key={c.id} className={c.visibility === 'Note' ? 'is-note' : undefined}>
            <div className="comment-meta">
              <b>{c.authorDisplayName}</b> · {dateTimeFr(c.createdAt)}
              {c.visibility === 'Note' && <span className="badge t-wait">Note de travail</span>}
              {(isAdmin || c.author.toLowerCase() === me) &&
                <button className="btn ghost small" style={{ marginLeft: 'auto' }} onClick={() => remove(c)} title="Retirer le commentaire">×</button>}
            </div>
            <p className="comment-body">{c.body}</p>
          </li>))}
      </ul>
      <div className="field">
        <label htmlFor={`comment-${type}-${id}`}>Ajouter un commentaire</label>
        <textarea id={`comment-${type}-${id}`} rows={3} value={body} maxLength={4000} onChange={e => setBody(e.target.value)}
          placeholder={note ? 'Note pour l\'équipe : invisible du demandeur.' : 'Visible de quiconque voit cet enregistrement, demandeur compris.'} />
      </div>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        {data.canWriteNotes && (
          <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontWeight: 400 }}>
            <input type="checkbox" checked={note} onChange={e => setNote(e.target.checked)} style={{ width: 'auto' }} />
            Note de travail (équipe)
          </label>)}
        <button className="btn" onClick={send} disabled={!body.trim()}>Publier</button>
      </div>
    </div>
  )
}
