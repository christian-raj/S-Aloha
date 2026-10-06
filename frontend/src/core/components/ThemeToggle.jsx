import React, { useState } from 'react'
import { IconMoon, IconSun } from './icons'

// Le thème initial est posé par le script en tête d'index.html, AVANT le
// premier rendu : décidé ici, il produirait un flash clair au chargement d'une
// session sombre. Ce composant ne fait que basculer la classe `dark`.
const STORAGE_KEY = 's-aloha-theme'

export default function ThemeToggle() {
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'))

  const toggle = () => {
    const next = !dark
    document.documentElement.classList.toggle('dark', next)
    try {
      localStorage.setItem(STORAGE_KEY, next ? 'dark' : 'light')
    } catch {
      /* stockage indisponible (navigation privée…) — le thème reste en mémoire */
    }
    setDark(next)
  }

  return (
    <button type="button" className="icon-btn" onClick={toggle}
      aria-label={dark ? 'Passer en mode clair' : 'Passer en mode sombre'}
      title={dark ? 'Mode clair' : 'Mode sombre'}>
      {dark ? <IconSun /> : <IconMoon />}
    </button>
  )
}
