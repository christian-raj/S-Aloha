import React, { useEffect, useState } from 'react'
import { api } from '../../api'
import SearchHits from './SearchHits'

/**
 * Cas similaires (RAG-06) : articles publiés, problèmes et incidents résolus
 * proches de cet enregistrement — pour ne pas réinventer une solution déjà
 * trouvée. Échec silencieux : la fiche reste utilisable sans la recherche.
 */
export default function SimilarCases({ type, id, refreshKey }) {
  const [hits, setHits] = useState(null)
  useEffect(() => { api.search.similar(type, id).then(setHits).catch(() => setHits([])) }, [type, id, refreshKey])
  return (
    <div className="card" style={{ marginTop: 16 }}>
      <h3 style={{ marginBottom: 4 }}>Cas similaires</h3>
      <p className="muted small" style={{ marginBottom: 10 }}>Solutions publiées, erreurs connues et incidents résolus proches de celui-ci.</p>
      {hits === null ? <p className="muted small">Recherche…</p>
        : <SearchHits hits={hits} empty="Aucun cas similaire dans la base de connaissances." />}
    </div>
  )
}
