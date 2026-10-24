import React from 'react'

/**
 * Garde d'erreur : une exception au rendu affiche un message au lieu de
 * démonter toute l'application (page blanche). Deux niveaux :
 * - `page` (Layout, autour de la page ouverte) : la navigation reste
 *   utilisable ; la garde se réarme au changement de page (`key` posée par
 *   l'appelant) ;
 * - `app` (main.jsx) : dernier recours, si la coque elle-même échoue.
 * Le détail technique reste dans un volet replié, pour le support.
 */
export default class ErrorBoundary extends React.Component {
  state = { error: null }

  static getDerivedStateFromError(error) { return { error } }

  componentDidCatch(error, info) { console.error('Erreur d’affichage', error, info?.componentStack) }

  render() {
    const { error } = this.state
    if (!error) return this.props.children
    const app = this.props.level === 'app'
    return (
      <div className={'crash' + (app ? ' app' : '')} role="alert">
        <h1 className="crash-title">{app ? 'S-Aloha n’a pas pu s’afficher' : 'Cette page n’a pas pu s’afficher'}</h1>
        <p className="crash-text">
          Une erreur inattendue l&rsquo;a interrompue. Aucune donnée n&rsquo;est perdue :
          {app ? ' rechargez la page.' : ' rechargez la page, ou continuez ailleurs avec le menu.'}
        </p>
        <div className="crash-actions">
          <button type="button" className="btn" onClick={() => window.location.reload()}>Recharger la page</button>
          {/* Lien simple, pas <Link> : la garde racine est hors du routeur. */}
          <a className="btn ghost" href="/">Retour à la console</a>
        </div>
        <details className="crash-details">
          <summary>Détail technique</summary>
          <pre>{String(error?.message ?? error)}</pre>
        </details>
      </div>
    )
  }
}
