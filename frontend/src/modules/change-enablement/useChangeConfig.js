import { useEffect, useMemo, useState } from 'react'
import { api } from '../../api'
import { changeConfig, standardModelField } from './config'

/** Configuration des changements, avec le choix du modèle standard (CHG-26) après le type. */
export default function useChangeConfig() {
  const [models, setModels] = useState(null)
  useEffect(() => { Promise.resolve().then(() => api.standardChanges.list()).then(setModels).catch(() => setModels([])) }, [])
  return useMemo(() => models && {
    ...changeConfig,
    fields: changeConfig.fields.flatMap(f => (f.key === 'changeType' ? [f, standardModelField(models)] : [f])),
  }, [models])
}
