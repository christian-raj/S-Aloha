import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../api'
import { PILLARS } from '../../modules/registry'
import { BrandMark } from '../components/icons'
import { SOURCE_URL, LICENSE_LABEL } from '../about'

export default function Login() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const nav = useNavigate()

  const submit = async (e) => {
    e.preventDefault()
    setError(''); setBusy(true)
    try {
      const res = await api.login(username, password)
      sessionStorage.setItem('token', res.token)
      sessionStorage.setItem('user', JSON.stringify(res))
      nav('/')
    } catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  return (
    <div className="login">
      <aside className="login-hero">
        <div className="login-glow a" aria-hidden />
        <div className="login-glow b" aria-hidden />

        <div className="brand">
          <div className="brand-mark large"><BrandMark size={24} /></div>
          <span className="brand-name large">S-Aloha</span>
        </div>

        <div>
          <h1>L'excellence du service IT au cœur de notre performance.</h1>
          <p className="login-motto">« Ny fahaiza-manao ho amin'ny tolotra tsara kokoa. »</p>
          <ul className="pillars">
            {PILLARS.map((p) => (
              <li key={p.id}>
                <span className="pillar-letter">{p.letter}</span>
                <span><b>{p.label}</b><small>{p.desc}</small></span>
              </li>
            ))}
          </ul>
        </div>

        <p className="login-foot">Service alohan'ny zavatra rehetra</p>
      </aside>

      <div className="login-side bg-canvas">
        <form className="login-card" onSubmit={submit}>
          <div className="login-compact-brand">
            <div className="brand-mark large"><BrandMark size={24} /></div>
            <div>
              <p className="brand-name large">S-Aloha</p>
              <p className="muted small">Excellence du service IT</p>
            </div>
          </div>
          <div>
            <h2>Connexion</h2>
            <p className="muted small">Avec votre compte Active Directory.</p>
          </div>
          {error && <div className="error">{error}</div>}
          <div className="field">
            <label htmlFor="username">Identifiant</label>
            <input id="username" value={username} onChange={e => setUsername(e.target.value)}
              placeholder="prenom.nom" autoFocus autoComplete="username" required />
          </div>
          <div className="field">
            <label htmlFor="password">Mot de passe</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)}
              autoComplete="current-password" required />
          </div>
          <button className="btn block" type="submit" disabled={busy}>
            {busy ? 'Connexion…' : 'Se connecter'}
          </button>
          <a className="source-link muted" href={SOURCE_URL} target="_blank" rel="noreferrer">Code source · {LICENSE_LABEL}</a>
        </form>
      </div>
    </div>
  )
}
