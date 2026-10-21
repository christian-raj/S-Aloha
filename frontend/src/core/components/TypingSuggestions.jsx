import React, { useEffect, useState } from 'react'
import { api } from '../../api'
import SearchHits from './SearchHits'

/**
 * RAG-11 : cas similaires dès la saisie du titre d'un incident ou d'une demande — une
 * solution publiée ou un incident déjà résolu évite parfois de créer l'enregistrement.
 * Requête après une courte pause de frappe ; échec silencieux.
 */
export default function TypingSuggestions({ text, delay = 400 }) {
  const [hits, setHits] = useState([])
  const query = (text ?? '').trim()
  useEffect(() => {
    if (query.length < 4) { setHits([]); return }
    const timer = setTimeout(() => {
      Promise.resolve().then(() => api.search.query(query)).then(h => setHits(h.slice(0, 5))).catch(() => setHits([]))
    }, delay)
    return () => clearTimeout(timer)
  }, [query, delay])
  if (hits.length === 0) return null
  return (
    <div className="card suggestions" style={{ margin: '0 0 14px' }} aria-label="Cas similaires">
      <p className="small" style={{ marginBottom: 6 }}><b>Déjà vu ?</b> Des cas proches existent : une solution y figure peut-être.</p>
      <SearchHits hits={hits} />
    </div>
  )
}
