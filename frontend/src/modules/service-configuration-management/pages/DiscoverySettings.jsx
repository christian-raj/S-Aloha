import React, { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, getUser } from '../../../api'
import { dateFr, dateTimeFr } from '../../../core/fields'
import PerimeterSelect from '../../../core/components/PerimeterSelect'
import usePerimeters, { perimeterName } from '../../../core/usePerimeters'
import { JOB_TONES, SOURCE_LABELS } from '../config'

/** Types de scan : cible attendue et identifiant du coffre requis. */
const SCAN_TYPES = {
  IpSweep: { label: 'Balayage IP', target: 'Réseau CIDR (/24 au plus) ou adresse, ex. 192.168.1.0/24', credential: null,
    help: 'Ping et sondage TCP des ports témoins, nom par DNS inverse. Sans identifiant.' },
  Snmp: { label: 'SNMP v2c', target: 'Réseau CIDR (/24 au plus) ou adresse', credential: 'SnmpV2c',
    help: 'Lecture seule (sysDescr, sysName, sysLocation, n° de série) avec une communauté du coffre.' },
  ActiveDirectory: { label: 'Active Directory', target: 'BaseDN facultatif (sinon celle de l’annuaire configuré)', credential: 'WindowsDomain',
    help: 'Comptes d’ordinateur actifs. Compte de domaine du coffre facultatif (sinon le compte de service de l’API).' },
}
const CREDENTIAL_TYPES = {
  SnmpV2c: { label: 'Communauté SNMP v2c', secret: 'Communauté' },
  WindowsDomain: { label: 'Compte de domaine Windows', secret: 'Mot de passe' },
}
const BUSY = ['En file', 'En cours']
const blankJob = { name: '', scanType: 'IpSweep', targetRange: '', portList: '', credentialId: '', frequencyHours: 24,
  windowStart: '20:00', windowEnd: '06:00', isActive: true }

function JobForm({ job, credentials, onSaved, onCancel }) {
  const perimeters = usePerimeters().filter(p => ['Admin', 'Manager'].includes(p.role))
  const [f, setF] = useState(() => job ? { ...blankJob, ...job, credentialId: job.credentialId ?? '', portList: job.portList ?? '',
    windowStart: job.windowStart ?? '', windowEnd: job.windowEnd ?? '' } : blankJob)
  const [perimeterId, setPerimeterId] = useState(job?.perimeterId ?? null)
  const [error, setError] = useState('')
  // Un seul périmètre géré (parmi d'autres consultés) : pas de choix à faire, il est retenu d'office.
  useEffect(() => { if (!job && perimeterId === null && perimeters.length === 1) setPerimeterId(perimeters[0].id) }, [perimeters.length])
  const set = patch => setF(prev => ({ ...prev, ...patch }))
  const type = SCAN_TYPES[f.scanType]
  const usable = credentials.filter(c => c.credentialType === type.credential && (!perimeterId || c.perimeterId === perimeterId))
  const save = () => {
    const dto = { ...f, credentialId: f.credentialId ? Number(f.credentialId) : null, frequencyHours: Number(f.frequencyHours) || 0,
      windowStart: f.windowStart || null, windowEnd: f.windowEnd || null, perimeterId }
    ;(job ? api.discovery.jobs.update(job.id, dto) : api.discovery.jobs.create(dto)).then(onSaved).catch(e => setError(e.message))
  }
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <h3 className="card-title">{job ? `Modifier « ${job.name} »` : 'Nouvelle tâche de scan'}</h3>
      {error && <div className="error" role="alert">{error}</div>}
      {!job && <PerimeterSelect perimeters={perimeters} value={perimeterId} onChange={setPerimeterId} required />}
      <div className="form-grid">
        <div className="field"><label>Nom *</label>
          <input value={f.name} onChange={e => set({ name: e.target.value })} placeholder="Ex. : Réseau du siège" /></div>
        <div className="field"><label>Type de scan *</label>
          <select value={f.scanType} onChange={e => set({ scanType: e.target.value, credentialId: '' })}>
            {Object.entries(SCAN_TYPES).map(([k, t]) => <option key={k} value={k}>{t.label}</option>)}
          </select>
          <small className="field-hint">{type.help}</small></div>
        <div className="field full"><label>Cible{f.scanType !== 'ActiveDirectory' && ' *'}</label>
          <input value={f.targetRange} onChange={e => set({ targetRange: e.target.value })} placeholder={type.target} /></div>
        {f.scanType === 'IpSweep' && <div className="field full"><label>Ports TCP sondés</label>
          <input value={f.portList} onChange={e => set({ portList: e.target.value })} placeholder="Par défaut : 22,80,443,135,445,3389,5985,9100" />
          <small className="field-hint">Séparés par des virgules, 50 au plus.</small></div>}
        {type.credential && <div className="field"><label>Identifiant du coffre{f.scanType === 'Snmp' && ' *'}</label>
          <select value={f.credentialId} onChange={e => set({ credentialId: e.target.value })}>
            <option value="">{f.scanType === 'Snmp' ? '— Choisir —' : 'Compte de service de l’API'}</option>
            {usable.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          {usable.length === 0 && <small className="field-hint">Aucun identifiant « {CREDENTIAL_TYPES[type.credential].label} » : l’Admin l’ajoute dans l’onglet Identifiants.</small>}
        </div>}
        <div className="field"><label>Fréquence (heures)</label>
          <input type="number" min="0" value={f.frequencyHours} onChange={e => set({ frequencyHours: e.target.value })} />
          <small className="field-hint">0 : lancement manuel seulement.</small></div>
        <div className="field"><label>Fenêtre de scan</label>
          <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input type="time" value={f.windowStart} onChange={e => set({ windowStart: e.target.value })} aria-label="Début de la fenêtre" />
            à <input type="time" value={f.windowEnd} onChange={e => set({ windowEnd: e.target.value })} aria-label="Fin de la fenêtre" />
          </span>
          <small className="field-hint">Heure locale ; hors heures ouvrées de préférence. Vide : à toute heure.</small></div>
        <div className="field"><label>État</label>
          <label className="check"><input type="checkbox" checked={f.isActive} onChange={e => set({ isActive: e.target.checked })} /> Active (lancements planifiés)</label></div>
      </div>
      <div className="actions">
        <button className="btn" onClick={save} disabled={!f.name.trim()}>Enregistrer</button>
        <button className="btn ghost" onClick={onCancel}>Annuler</button>
      </div>
    </div>
  )
}

/** Tâches de scan (CFG-46) : planification, lancement immédiat, suivi du dernier passage. */
function Jobs({ isAdmin }) {
  const [jobs, setJobs] = useState(null)
  const [credentials, setCredentials] = useState([])
  const [editing, setEditing] = useState(null)       // null, 'new' ou une tâche
  const [error, setError] = useState('')
  const perimeters = usePerimeters()
  const timer = useRef(null)
  const load = () => api.discovery.jobs.list().then(setJobs).catch(e => setError(e.message))
  useEffect(() => {
    load()
    api.discovery.credentials.list().then(r => setCredentials(r.items)).catch(() => setCredentials([]))
  }, [])
  // Un scan en file ou en cours : l'écran se rafraîchit jusqu'à la fin.
  useEffect(() => {
    clearTimeout(timer.current)
    if (jobs?.some(j => BUSY.includes(j.lastStatus))) timer.current = setTimeout(load, 3000)
    return () => clearTimeout(timer.current)
  }, [jobs])

  const run = j => api.discovery.jobs.run(j.id).then(load).catch(e => setError(e.message))
  const remove = j => window.confirm(`Supprimer la tâche « ${j.name} » ? Les actifs déjà découverts restent dans le sas.`)
    && api.discovery.jobs.remove(j.id).then(load).catch(e => setError(e.message))
  const saved = () => { setEditing(null); load() }

  return (
    <>
      {error && <div className="error" role="alert">{error}</div>}
      {editing ? <JobForm job={editing === 'new' ? null : editing} credentials={credentials} onSaved={saved} onCancel={() => setEditing(null)} />
        : <div className="toolbar"><button className="btn" onClick={() => setEditing('new')}>+ Nouvelle tâche de scan</button>
            <Link className="btn ghost" style={{ marginLeft: 'auto' }} to="/configuration/staging">Ouvrir le sas de staging</Link></div>}
      {jobs?.length === 0 && !editing && (
        <div className="card empty-state">
          <h3>Aucune tâche de scan</h3>
          <p className="muted">Commencez par un balayage IP d’un réseau de /24 : les équipements trouvés arrivent dans le sas de staging, sans rien modifier dans la CMDB.</p>
          <button className="btn" onClick={() => setEditing('new')}>Créer une première tâche</button>
        </div>
      )}
      {jobs?.length > 0 && (
        <div className="table-scroll">
          <table>
            <thead><tr><th>Tâche</th><th>Cible</th><th>Planification</th><th>Dernier passage</th><th></th></tr></thead>
            <tbody>{jobs.map(j => (
              <tr key={j.id}>
                <td><b>{j.name}</b><div className="muted small">{SCAN_TYPES[j.scanType]?.label ?? j.scanType}
                  {j.credential && ` · ${j.credential.name}`}{perimeters.length > 1 && ` · ${perimeterName(perimeters, j.perimeterId)}`}</div></td>
                <td className="mono small">{j.targetRange || 'Annuaire configuré'}</td>
                <td className="small">{!j.isActive ? 'Désactivée' : j.frequencyHours === 0 ? 'Manuelle' : `Toutes les ${j.frequencyHours} h`}
                  {j.isActive && j.frequencyHours > 0 && j.windowStart && <div className="muted">entre {j.windowStart} et {j.windowEnd}</div>}</td>
                <td><span className={'badge ' + (JOB_TONES[j.lastStatus] ?? 't-closed')}>{j.lastStatus}</span>
                  {j.lastRunAt && <div className="muted small">{dateTimeFr(j.lastRunAt)}{j.lastFoundCount != null && ` · ${j.lastFoundCount} équipement(s)`}</div>}
                  {j.lastError && <div className="small" style={{ color: 'var(--accent-red)' }}>{j.lastError}</div>}</td>
                <td className="row-actions">
                  <button className="btn small" onClick={() => run(j)} disabled={BUSY.includes(j.lastStatus)}>
                    {BUSY.includes(j.lastStatus) ? j.lastStatus + '…' : 'Lancer'}</button>
                  <button className="btn ghost small" onClick={() => setEditing(j)}>Modifier</button>
                  {isAdmin && <button className="btn ghost small" onClick={() => remove(j)} aria-label={`Supprimer ${j.name}`}>Supprimer</button>}
                </td>
              </tr>))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}

function CredentialForm({ credential, onSaved, onCancel }) {
  const perimeters = usePerimeters()
  const [f, setF] = useState({ name: credential?.name ?? '', credentialType: credential?.credentialType ?? 'SnmpV2c',
    username: credential?.username ?? '', domain: credential?.domain ?? '', secret: '' })
  const [perimeterId, setPerimeterId] = useState(null)
  const [error, setError] = useState('')
  const set = patch => setF(prev => ({ ...prev, ...patch }))
  const kind = CREDENTIAL_TYPES[f.credentialType]
  const save = () => (credential ? api.discovery.credentials.update(credential.id, f)
    : api.discovery.credentials.create({ ...f, perimeterId })).then(onSaved).catch(e => setError(e.message))
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <h3 className="card-title">{credential ? `Modifier « ${credential.name} »` : 'Nouvel identifiant'}</h3>
      {error && <div className="error" role="alert">{error}</div>}
      {!credential && <PerimeterSelect perimeters={perimeters} value={perimeterId} onChange={setPerimeterId} required />}
      <div className="form-grid">
        <div className="field"><label>Nom *</label>
          <input value={f.name} onChange={e => set({ name: e.target.value })} placeholder="Ex. : Communauté SNMP du siège" /></div>
        <div className="field"><label>Type *</label>
          <select value={f.credentialType} onChange={e => set({ credentialType: e.target.value })} disabled={credential?.usedBy > 0}>
            {Object.entries(CREDENTIAL_TYPES).map(([k, t]) => <option key={k} value={k}>{t.label}</option>)}
          </select></div>
        {f.credentialType === 'WindowsDomain' && <>
          <div className="field"><label>Compte *</label>
            <input value={f.username} onChange={e => set({ username: e.target.value })} placeholder="svc-decouverte" autoComplete="off" /></div>
          <div className="field"><label>Domaine</label>
            <input value={f.domain} onChange={e => set({ domain: e.target.value })} placeholder="MONDOMAINE" autoComplete="off" /></div>
        </>}
        <div className="field"><label>{kind.secret}{!credential && ' *'}</label>
          <input type="password" value={f.secret} onChange={e => set({ secret: e.target.value })} autoComplete="new-password"
            placeholder={credential ? 'Laisser vide pour conserver l’actuel' : ''} />
          <small className="field-hint">Chiffré dès l’enregistrement ; jamais réaffiché.</small></div>
      </div>
      <div className="actions">
        <button className="btn" onClick={save} disabled={!f.name.trim() || (!credential && !f.secret)}>Enregistrer</button>
        <button className="btn ghost" onClick={onCancel}>Annuler</button>
      </div>
    </div>
  )
}

/** Coffre d'identifiants (CFG-45), Admin : communautés SNMP, comptes de domaine. */
function Credentials() {
  const [data, setData] = useState(null)
  const [editing, setEditing] = useState(null)
  const [error, setError] = useState('')
  const load = () => api.discovery.credentials.list().then(setData).catch(e => setError(e.message))
  useEffect(() => { load() }, [])
  const remove = c => window.confirm(`Supprimer « ${c.name} » du coffre ?`)
    && api.discovery.credentials.remove(c.id).then(load).catch(e => setError(e.message))
  if (!data) return error ? <div className="error">{error}</div> : <p>Chargement…</p>
  return (
    <>
      {error && <div className="error" role="alert">{error}</div>}
      {!data.vaultConfigured && <div className="notice warn">
        Le coffre n’est pas configuré : définir <code>Discovery:VaultKey</code> (32 caractères au moins) dans la configuration de l’API
        avant d’enregistrer un identifiant. Ne plus la changer ensuite : les secrets déjà chiffrés deviendraient illisibles.
      </div>}
      {editing ? <CredentialForm credential={editing === 'new' ? null : editing} onSaved={() => { setEditing(null); load() }} onCancel={() => setEditing(null)} />
        : <div className="toolbar"><button className="btn" onClick={() => setEditing('new')} disabled={!data.vaultConfigured}>+ Nouvel identifiant</button></div>}
      <div className="table-scroll">
        <table>
          <thead><tr><th>Identifiant</th><th>Type</th><th>Compte</th><th>Secret</th><th>Utilisé par</th><th></th></tr></thead>
          <tbody>
            {data.items.map(c => (
              <tr key={c.id}>
                <td><b>{c.name}</b></td>
                <td>{CREDENTIAL_TYPES[c.credentialType]?.label ?? c.credentialType}</td>
                <td>{c.username ? `${c.domain ? c.domain + '\\' : ''}${c.username}` : '—'}</td>
                <td className="small">{c.hasSecret ? <>Enregistré, chiffré{c.secretChangedAt && <div className="muted">changé le {dateFr(c.secretChangedAt)}</div>}</> : 'Absent'}</td>
                <td>{c.usedBy ? `${c.usedBy} tâche(s)` : '—'}</td>
                <td className="row-actions">
                  <button className="btn ghost small" onClick={() => setEditing(c)}>Modifier ou remplacer le secret</button>
                  <button className="btn ghost small" onClick={() => remove(c)} disabled={c.usedBy > 0}
                    title={c.usedBy > 0 ? 'Utilisé par des tâches de scan' : undefined}>Supprimer</button>
                </td>
              </tr>))}
            {data.items.length === 0 && <tr><td colSpan={6} className="muted">Aucun identifiant. Un scan SNMP exige une communauté ; un scan de l’annuaire peut utiliser un compte de domaine dédié.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  )
}

/** Fins de support (CFG-47) : produits et date de fin de support, rapprochés du système des CI. */
function EndOfLife({ canEdit }) {
  const [rules, setRules] = useState(null)
  const [f, setF] = useState({ product: '', endOfSupport: '', notes: '' })
  const [error, setError] = useState('')
  const load = () => api.discovery.endOfLife.list().then(setRules).catch(e => setError(e.message))
  useEffect(() => { load() }, [])
  const add = () => api.discovery.endOfLife.create({ ...f, endOfSupport: f.endOfSupport || null })
    .then(() => { setF({ product: '', endOfSupport: '', notes: '' }); setError(''); load() }).catch(e => setError(e.message))
  const remove = r => window.confirm(`Retirer la fin de support de « ${r.product} » ?`)
    && api.discovery.endOfLife.remove(r.id).then(load).catch(e => setError(e.message))
  const today = new Date().toISOString().slice(0, 10)
  return (
    <>
      <p className="muted small" style={{ marginBottom: 12 }}>
        Un CI dont le système, le constructeur ou le modèle contient le produit est en fin de support à cette date ; il apparaît
        dans l’inventaire et le tableau de bord (obsolescence, NIS 2). Nommer le produit comme il figure dans le système des CI.
      </p>
      {error && <div className="error" role="alert">{error}</div>}
      {canEdit && (
        <div className="toolbar">
          <input style={{ maxWidth: 280 }} placeholder="Produit, ex. Windows Server 2012" value={f.product}
            onChange={e => setF({ ...f, product: e.target.value })} aria-label="Produit" />
          <input type="date" style={{ maxWidth: 170 }} value={f.endOfSupport} onChange={e => setF({ ...f, endOfSupport: e.target.value })} aria-label="Fin de support" />
          <input style={{ maxWidth: 260 }} placeholder="Note (facultatif)" value={f.notes} onChange={e => setF({ ...f, notes: e.target.value })} aria-label="Note" />
          <button className="btn" onClick={add} disabled={!f.product.trim() || !f.endOfSupport}>Ajouter</button>
        </div>
      )}
      <div className="table-scroll">
        <table className="compact">
          <thead><tr><th>Produit</th><th>Fin de support</th><th>Note</th><th></th></tr></thead>
          <tbody>
            {rules?.map(r => (
              <tr key={r.id}>
                <td><b>{r.product}</b></td>
                <td>{dateFr(r.endOfSupport)} {r.endOfSupport.slice(0, 10) < today && <span className="badge t-bad">Dépassée</span>}</td>
                <td className="small">{r.notes ?? '—'}</td>
                <td className="row-actions">{canEdit && <button className="btn ghost small" onClick={() => remove(r)}>Retirer</button>}</td>
              </tr>))}
            {rules?.length === 0 && <tr><td colSpan={4} className="muted">Aucune fin de support enregistrée.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  )
}

/**
 * Paramétrage → Découverte du réseau (Manager, Admin) : tâches de scan, coffre
 * d'identifiants (Admin) et référentiel des fins de support.
 */
export default function DiscoverySettings() {
  const [params, setParams] = useSearchParams()
  const role = getUser()?.role
  const isAdmin = role === 'Admin'
  const tabs = [
    { id: 'jobs', label: 'Tâches de scan' },
    isAdmin && { id: 'credentials', label: 'Identifiants (coffre)' },
    { id: 'eol', label: 'Fins de support' },
  ].filter(Boolean)
  const tab = tabs.find(t => t.id === params.get('tab')) ?? tabs[0]
  return (
    <>
      <h1 className="page-title">Découverte du réseau</h1>
      <p className="page-sub">
        Ce que la découverte parcourt ({Object.values(SOURCE_LABELS).join(', ')}), avec quels identifiants et à quelle heure ;
        les équipements trouvés attendent dans le sas de staging.
      </p>
      <div className="tabs" role="tablist">
        {tabs.map(t => <button key={t.id} role="tab" aria-selected={t.id === tab.id} className={t.id === tab.id ? 'on' : ''}
          onClick={() => setParams(t.id === 'jobs' ? {} : { tab: t.id }, { replace: true })}>{t.label}</button>)}
      </div>
      {tab.id === 'jobs' && <Jobs isAdmin={isAdmin} />}
      {tab.id === 'credentials' && <Credentials />}
      {tab.id === 'eol' && <EndOfLife canEdit={['Admin', 'Manager'].includes(role)} />}
    </>
  )
}
