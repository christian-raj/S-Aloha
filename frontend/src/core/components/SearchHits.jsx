import React from 'react'
import { Link } from 'react-router-dom'
import { ITEM_TYPES, hrefOf } from '../itemTypes'

/** Comment l'élément a été trouvé : par les mots (lexical), par le sens (vectoriel), ou les deux. */
function How({ hit }) {
  const label = hit.lexical && hit.semantic ? 'mots et sens' : hit.lexical ? 'mots' : 'sens'
  return <span className="muted small" title="Correspondance trouvée par les mots du texte, par la proximité de sens, ou les deux">{label}</span>
}

/** Résultats de la recherche hybride : type, référence, titre, extrait. */
export default function SearchHits({ hits, empty = 'Aucun résultat.' }) {
  if (hits.length === 0) return <p className="muted small">{empty}</p>
  return (
    <ul className="search-hits">
      {hits.map(h => (
        <li key={h.type + h.id}>
          <div>
            <span className="badge t-closed">{ITEM_TYPES[h.type]?.label ?? h.type}</span>{' '}
            <Link to={hrefOf(h.type, h.id)}><b>{h.reference}</b> {h.title}</Link>{' '}
            <span className="muted small">· {h.status} · </span><How hit={h} />
          </div>
          <p className="small" style={{ whiteSpace: 'pre-line', margin: '4px 0 0' }}>{h.snippet}</p>
        </li>
      ))}
    </ul>
  )
}
