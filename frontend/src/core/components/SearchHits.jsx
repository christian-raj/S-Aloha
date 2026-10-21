import React, { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api'
import { ITEM_TYPES, hrefOf } from '../itemTypes'

/** Comment l'élément a été trouvé : par les mots (lexical), par le sens (vectoriel), ou les deux. */
function How({ hit }) {
  const label = hit.lexical && hit.semantic ? 'mots et sens' : hit.lexical ? 'mots' : 'sens'
  return <span className="muted small" title="Correspondance trouvée par les mots du texte, par la proximité de sens, ou les deux">{label}</span>
}

/** RAG-09 : relier en un clic l'article ou le problème retenu à l'enregistrement d'origine. */
function LinkButton({ from, hit }) {
  const [state, setState] = useState(null)   // null, 'ok', message d'erreur
  const link = () => api.links.create({ fromType: from.type, fromId: from.id, toReference: hit.reference })
    .then(() => setState('ok')).catch(e => setState(/existe déjà/.test(e.message) ? 'ok' : e.message))
  if (state === 'ok') return <span className="badge t-done">Relié</span>
  return <>
    <button className="btn ghost small" onClick={link}>Relier</button>
    {state && <span className="error-inline small"> {state}</span>}
  </>
}

/**
 * Résultats de la recherche hybride : type, référence, titre, extrait. `linkFrom` ({ type, id }) :
 * un bouton « Relier » sur les articles et les problèmes (RAG-09).
 */
export default function SearchHits({ hits, empty = 'Aucun résultat.', linkFrom }) {
  if (hits.length === 0) return <p className="muted small">{empty}</p>
  return (
    <ul className="search-hits">
      {hits.map(h => (
        <li key={h.type + h.id}>
          <div>
            <span className="badge t-closed">{ITEM_TYPES[h.type]?.label ?? h.type}</span>{' '}
            <Link to={hrefOf(h.type, h.id)}><b>{h.reference}</b> {h.title}</Link>{' '}
            <span className="muted small">· {h.status} · </span><How hit={h} />
            {linkFrom && ['article', 'problem'].includes(h.type) && <> <LinkButton from={linkFrom} hit={h} /></>}
          </div>
          <p className="small" style={{ whiteSpace: 'pre-line', margin: '4px 0 0' }}>{h.snippet}</p>
        </li>
      ))}
    </ul>
  )
}
