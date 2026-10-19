import React, { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../../api'

const TABS = [{ id: 'incident', label: 'Incidents' }, { id: 'problem', label: 'Problèmes' }]

/** Une ligne : renommer, (dés)activer, supprimer tant qu'elle n'est pas utilisée (ADM-50, ADM-53). */
function Row({ c, onSaved, onError }) {
  const [name, setName] = useState(c.name)
  const save = dto => api.categories.update(c.id, { practice: c.practice, name: c.name, active: c.active, ...dto }).then(onSaved).catch(e => onError(e.message))
  const remove = () => api.categories.remove(c.id).then(onSaved).catch(e => onError(e.message))
  return (
    <tr className={c.active ? '' : 'muted'}>
      <td>
        <form className="inline-form" onSubmit={e => { e.preventDefault(); if (name.trim() && name !== c.name) save({ name }) }}>
          <input value={name} onChange={e => setName(e.target.value)} aria-label={`Nom de la catégorie ${c.name}`} maxLength={100} />
          {name !== c.name && <button className="btn ghost small">Renommer</button>}
        </form>
      </td>
      <td>{c.usage}</td>
      <td>{c.active ? 'Active' : 'Désactivée'}</td>
      <td className="actions">
        <button className="btn ghost small" onClick={() => save({ active: !c.active })}>{c.active ? 'Désactiver' : 'Réactiver'}</button>
        {c.usage === 0 && <button className="btn small danger" onClick={remove}>Supprimer</button>}
      </td>
    </tr>
  )
}

/**
 * Paramétrage → Catégories (ADM-50 à ADM-53, SOC-27) : la liste des catégories d'incident et de
 * problème. Dès qu'une pratique a une catégorie active, la saisie se fait dans la liste.
 */
export default function Categories() {
  const [params, setParams] = useSearchParams()
  const tab = TABS.find(t => t.id === params.get('practice')) ?? TABS[0]
  const [list, setList] = useState(null)
  const [name, setName] = useState('')
  const [error, setError] = useState('')
  const load = () => api.categories.list(tab.id).then(l => { setList(l); setError('') }).catch(e => setError(e.message))
  useEffect(() => { setList(null); load() }, [tab.id])
  const add = e => {
    e.preventDefault()
    api.categories.create({ practice: tab.id, name }).then(() => { setName(''); load() }).catch(err => setError(err.message))
  }
  const active = list?.filter(c => c.active).length ?? 0

  return (
    <>
      <h1 className="page-title">Catégories</h1>
      <p className="page-sub">
        Les catégories proposées à la saisie des incidents et des problèmes. Une catégorie utilisée ne se supprime pas :
        désactivée, elle reste sur les enregistrements qui la portent mais ne se choisit plus.
      </p>
      <div className="tabs" role="tablist">
        {TABS.map(t => <button key={t.id} role="tab" aria-selected={t.id === tab.id} className={t.id === tab.id ? 'on' : ''}
          onClick={() => setParams(t.id === 'incident' ? {} : { practice: t.id }, { replace: true })}>{t.label}</button>)}
      </div>
      {error && <div className="error" role="alert">{error}</div>}
      <section className="card">
        <p className="muted small">
          {active === 0
            ? 'Aucune catégorie active : la catégorie se saisit librement.'
            : `${active} catégorie(s) active(s) : la catégorie se choisit dans la liste.`}
        </p>
        <form className="inline-form" onSubmit={add}>
          <input value={name} onChange={e => setName(e.target.value)} placeholder="Ex. : Réseau" aria-label="Nouvelle catégorie" maxLength={100} />
          <button className="btn" disabled={!name.trim()}>Ajouter</button>
        </form>
        {!list && !error && <p>Chargement…</p>}
        {list?.length > 0 && (
          <div className="table-scroll">
            <table>
              <thead><tr><th>Catégorie</th><th>Utilisations</th><th>État</th><th></th></tr></thead>
              <tbody>{list.map(c => <Row key={`${c.id}-${c.name}-${c.active}`} c={c} onSaved={load} onError={setError} />)}</tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
