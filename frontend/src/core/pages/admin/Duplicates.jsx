import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../../api'
import { dateFr } from '../../fields'
import { ITEM_TYPES, hrefOf } from '../../itemTypes'
import DeleteDialog from '../../components/DeleteDialog'

/** Un enregistrement d'une paire : référence, titre, statut, ce qu'il porte. */
function SideCard({ type, side }) {
  return (
    <div className="dup-side">
      <Link to={hrefOf(type, side.id)}><b>{side.reference}</b></Link> <span className="muted small">{side.status}</span>
      <p style={{ margin: '4px 0' }}>{side.title}</p>
      <p className="muted small">Créé le {dateFr(side.createdAt)} · {side.links} lien(s) · {side.comments} commentaire(s)</p>
    </div>
  )
}

/**
 * Administration → Nettoyage des doublons (ADM-40 à ADM-43) : paires probables, fusion dans
 * l'enregistrement conservé (liens et commentaires reportés), ou « pas un doublon ».
 */
export default function Duplicates() {
  const [pairs, setPairs] = useState(null)
  const [merging, setMerging] = useState(null)   // { pair, keep, remove }
  const [error, setError] = useState('')
  const [msg, setMsg] = useState('')
  const load = () => api.admin.duplicates().then(p => { setPairs(p); setError('') }).catch(e => setError(e.message))
  useEffect(() => { load() }, [])
  const dismiss = p => api.admin.dismissDuplicate({ type: p.type, firstId: p.first.id, secondId: p.second.id })
    .then(() => { setMsg(`Paire ${p.first.reference} / ${p.second.reference} écartée.`); load() }).catch(e => setError(e.message))
  const merge = () => api.admin.mergeDuplicate({ type: merging.pair.type, keepId: merging.keep.id, removeId: merging.remove.id })
    .then(r => { setMsg(`${merging.remove.reference} fusionné dans ${merging.keep.reference} : ${r.linksMoved} lien(s), ${r.commentsMoved} commentaire(s) reportés.`); setMerging(null); load() })

  return (
    <>
      <h1 className="page-title">Nettoyage des doublons</h1>
      <p className="page-sub">Enregistrements d'une même pratique et d'un même périmètre, au titre identique (casse, accents et ponctuation ignorés), créés à 7 jours d'intervalle au plus.</p>
      {error && <div className="error" role="alert">{error}</div>}
      {msg && <div className="notice ok" role="status">{msg}</div>}
      {!pairs && !error && <p>Recherche des doublons…</p>}
      {pairs?.length === 0 && (
        <div className="card empty-state"><h3>Aucun doublon probable</h3>
          <p className="muted">Incidents, demandes, problèmes, changements, articles et améliorations sont examinés.</p></div>
      )}
      {pairs?.map(p => (
        <section className="card dup-pair" key={`${p.type}-${p.first.id}-${p.second.id}`} aria-label={`Doublon probable ${p.first.reference} et ${p.second.reference}`}>
          <h3 className="card-title">{ITEM_TYPES[p.type]?.label ?? p.type}</h3>
          <div className="row2 stack-mobile">
            <SideCard type={p.type} side={p.first} />
            <SideCard type={p.type} side={p.second} />
          </div>
          <div className="actions" style={{ marginTop: 12 }}>
            <button className="btn" onClick={() => setMerging({ pair: p, keep: p.first, remove: p.second })}>Conserver {p.first.reference}</button>
            <button className="btn" onClick={() => setMerging({ pair: p, keep: p.second, remove: p.first })}>Conserver {p.second.reference}</button>
            <button className="btn ghost" style={{ marginLeft: 'auto' }} onClick={() => dismiss(p)}>Pas un doublon</button>
          </div>
        </section>
      ))}
      {merging && (
        <DeleteDialog
          heading={<>Fusionner <b>{merging.remove.reference}</b> dans <b>{merging.keep.reference}</b> ?</>}
          title={merging.keep.title}
          impactsLabel={`Reportés sur ${merging.keep.reference} :`}
          impacts={[{ label: 'lien(s)', count: merging.remove.links }, { label: 'commentaire(s)', count: merging.remove.comments }].filter(i => i.count > 0)}
          note={`${merging.remove.reference} est ensuite supprimé.`}
          confirmLabel="Fusionner"
          footnote="La fusion est définitive ; elle est tracée au journal d'audit des deux références."
          onConfirm={merge} onClose={() => setMerging(null)} />
      )}
    </>
  )
}
