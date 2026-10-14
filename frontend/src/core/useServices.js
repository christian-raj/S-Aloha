import { useEffect, useMemo, useState } from 'react'
import { api } from '../api'

/** Catalogue des services (SLM-10), chargé une fois ; null pendant le chargement. */
export default function useServices() {
  const [services, setServices] = useState(null)
  useEffect(() => { Promise.resolve().then(() => api.services.list()).then(setServices).catch(() => setServices([])) }, [])
  return services
}

/** Libellé d'un service dans une liste : un service retiré ne se choisit plus (SLM-15) mais reste lisible. */
export const serviceLabel = s => (s.status === 'Retiré' ? `${s.title} (retiré)` : s.title)

/** SLM-10, INC-20 : champ « service » choisi dans le catalogue, au lieu d'un texte libre. */
export const serviceField = (services, label = 'Service affecté') => ({
  key: 'serviceId', label, type: 'select', numeric: true,
  options: services.map(s => ({ value: s.id, label: serviceLabel(s) })),
})

/**
 * Configuration d'une pratique dont le service vient du catalogue : le champ texte
 * `affectedService` est remplacé par le choix du service, ou ajouté après `after`.
 */
export function withServices(config, services, after) {
  const field = serviceField(services)
  const fields = config.fields.some(f => f.key === 'affectedService')
    ? config.fields.map(f => (f.key === 'affectedService' ? field : f))
    : config.fields.flatMap(f => (f.key === after ? [f, field] : [f]))
  return { ...config, fields }
}

/** Hook : la configuration complétée du catalogue (null tant qu'il charge). */
export function useServiceConfig(config, after) {
  const services = useServices()
  return useMemo(() => (services ? withServices(config, services, after) : null), [services, config, after])
}
