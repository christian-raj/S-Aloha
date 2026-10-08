import React, { useState } from 'react'
import { api } from '../../api'
import SearchHits from './SearchHits'

/**
 * « Chercher d'abord » (KB-04, RAG-05) : une seule question interroge les
 * articles publiés, les erreurs connues et problèmes résolus, et les incidents
 * résolus — par les mots et par le sens.
 */
export default function KnowledgeSearch() {
  const [q, setQ] = useState('')
  const [hits, setHits] = useState(null)
  const [error, setError] = useState('')
  const run = (e) => {
    e.preventDefault()
    if (!q.trim()) { setHits(null); return }
    setError('')
    api.search.query(q.trim()).then(setHits).catch(err => setError(err.message))
  }
  return (
    <form className="card" style={{ marginBottom: 20 }} onSubmit={run} role="search">
      <h3 style={{ marginBottom: 4 }}>Chercher d'abord</h3>
      <p className="muted small" style={{ marginBottom: 10 }}>
        Décrivez le symptôme avec vos mots : la recherche couvre les articles publiés, les erreurs connues et les incidents résolus.</p>
      <div style={{ display: 'flex', gap: 10 }}>
        <input value={q} onChange={e => setQ(e.target.value)} placeholder="Ex. : les courriels mettent du temps à arriver le lundi"
          aria-label="Rechercher dans les connaissances" />
        <button className="btn" type="submit" disabled={!q.trim()}>Rechercher</button>
      </div>
      {error && <div className="error" style={{ marginTop: 10, marginBottom: 0 }}>{error}</div>}
      {hits && <div style={{ marginTop: 14 }}><SearchHits hits={hits} empty="Aucun résultat : essayez d'autres mots, ou créez l'article qui manque." /></div>}
    </form>
  )
}
