import { useEffect, useMemo, useState } from 'react'
import { api } from '../api'

/** ADM-50 : catégories d'une pratique (incident, problem), chargées une fois ; null pendant le chargement. */
export default function useCategories(practice) {
  const [categories, setCategories] = useState(null)
  useEffect(() => {
    Promise.resolve().then(() => api.categories.list(practice)).then(setCategories).catch(() => setCategories([]))
  }, [practice])
  return categories
}

/** Noms des catégories actives : les seules qu'on peut choisir (ADM-51). */
export const activeNames = categories => (categories ?? []).filter(c => c.active).map(c => c.name)

/**
 * ADM-51 : dès que la pratique a une liste, la catégorie se choisit dans la liste ; une valeur
 * déjà portée hors liste (désactivée, antérieure) reste affichée. Sans liste, saisie libre.
 */
export function withCategories(config, categories) {
  const names = activeNames(categories)
  if (names.length === 0) return config
  return {
    ...config,
    fields: config.fields.map(f => (f.key === 'category'
      ? { key: f.key, label: f.label, type: 'select', options: names, keepUnknown: v => `${v} (hors liste)` }
      : f))
  }
}

/** Hook : la configuration complétée des catégories (null tant que l'une ou l'autre charge). */
export function useCategoryConfig(config, practice) {
  const categories = useCategories(practice)
  return useMemo(() => (config && categories ? withCategories(config, categories) : null), [config, categories])
}
