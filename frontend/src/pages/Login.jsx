import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api'

export default function Login() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const nav = useNavigate()

  const submit = async () => {
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
    <div className="login-wrap">
      <div className="login-card">
        <h1>Gestion des problèmes</h1>
        <p>Connectez-vous avec votre compte Active Directory.</p>
        {error && <div className="error">{error}</div>}
        <div className="field">
          <label>Identifiant</label>
          <input value={username} onChange={e => setUsername(e.target.value)}
            placeholder="prenom.nom" autoFocus
            onKeyDown={e => e.key === 'Enter' && submit()} />
        </div>
        <div className="field">
          <label>Mot de passe</label>
          <input type="password" value={password} onChange={e => setPassword(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && submit()} />
        </div>
        <button className="btn" style={{ width: '100%' }} onClick={submit} disabled={busy}>
          {busy ? 'Connexion…' : 'Se connecter'}
        </button>
      </div>
    </div>
  )
}
