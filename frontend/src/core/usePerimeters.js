import { useEffect, useState } from 'react'
import { api } from '../api'

// Une seule requête par session de page : la liste sert à tous les formulaires.
let cache = null

/** Mes périmètres en service, avec mon rôle sur chacun (PER-03, PER-04). */
export default function usePerimeters() {
  const [list, setList] = useState(cache ?? [])
  useEffect(() => {
    // Sans liste (API indisponible, tests), aucun sélecteur : l'API retient le périmètre d'office.
    if (cache || !api.perimeters?.mine) return
    api.perimeters.mine().then(l => { cache = l; setList(l) }).catch(() => setList([]))
  }, [])
  return list
}

/** Pour les tests : oublie la liste chargée. */
export function resetPerimeters() { cache = null }

/** Nom d'un périmètre, ou à défaut son numéro. */
export const perimeterName = (list, id) => list.find(p => p.id === id)?.name ?? (id ? `Périmètre n° ${id}` : '')
