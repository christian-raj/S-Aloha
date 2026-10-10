import { useEffect, useState } from 'react'

/**
 * Graphe des transitions permises d'une pratique (SOC-05), servi par l'API :
 * { statut: [statuts accessibles] }. Null tant qu'il n'est pas chargé (ou si
 * l'API ne l'expose pas) : la liste des statuts reste alors complète, l'API
 * restant seule juge.
 */
export default function useTransitions(load) {
  const [graph, setGraph] = useState(null)
  useEffect(() => {
    if (!load) return
    let alive = true
    load().then(g => { if (alive) setGraph(g) }).catch(() => {})
    return () => { alive = false }
  }, [load])
  return graph
}

/** Le statut courant et ceux qui lui sont accessibles. */
export const reachable = (graph, current, status) =>
  status === current || !graph || (graph[current] ?? []).includes(status)
