import { useEffect, useMemo, useState } from 'react'
import { api } from '../../../api'
import { agreementConfig } from '../config'

/** Configuration des SLA, une fois le catalogue des services chargé (null avant). */
export default function useAgreementConfig() {
  const [services, setServices] = useState(null)
  useEffect(() => { api.services.list().then(setServices).catch(() => setServices([])) }, [])
  return useMemo(() => (services ? agreementConfig(services) : null), [services])
}
