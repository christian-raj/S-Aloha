import { useEffect, useMemo, useState } from 'react'
import { api } from '../../api'
import { withServices } from '../../core/useServices'
import { requestConfig, catalogField } from './config'

/** Configuration des demandes : modèle du catalogue (REQ-20) en tête, service du catalogue (SLM-10). */
export default function useRequestConfig() {
  const [data, setData] = useState(null)
  useEffect(() => {
    const safe = f => Promise.resolve().then(f).catch(() => [])
    Promise.all([safe(() => api.services.list()), safe(() => api.requestCatalog.list())])
      .then(([services, items]) => setData({ services, items }))
  }, [])
  return useMemo(() => {
    if (!data) return null
    const c = withServices(requestConfig, data.services, 'requestedItem')
    return { ...c, fields: [c.fields[0], catalogField(data.items), ...c.fields.slice(1)] }
  }, [data])
}
