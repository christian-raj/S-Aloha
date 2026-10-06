// Registre des processus ITIL couverts par S-Aloha — source unique de la
// navigation (barre latérale) et de la page de connexion. Un module passe de
// `soon` à `active` le jour où son interface existe : rien d'autre à toucher
// côté navigation. Cartographie et état : docs/reference/produit.md.

/** Les six piliers S-A-L-O-H-A. Deux « A » : l'identifiant, pas la lettre,
 * sert de clé. */
export const PILLARS = [
  { id: 'service', letter: 'S', label: 'Service', desc: 'Gestion des services et des incidents' },
  { id: 'alignement', letter: 'A', label: 'Alignment', desc: 'Processus IT alignés sur les besoins métier' },
  { id: 'leadership', letter: 'L', label: 'Leadership', desc: 'Accompagnement du changement et formation' },
  { id: 'optimisation', letter: 'O', label: 'Optimisation', desc: 'Amélioration continue (CSI)' },
  { id: 'harmonie', letter: 'H', label: 'Harmonie', desc: 'Cohésion entre équipes IT et utilisateurs' },
  { id: 'agilite', letter: 'A', label: 'Agilité', desc: 'Changements et mises en production fluides' },
]

/**
 * Processus ITIL, dans l'ordre du cycle de vie du service.
 * - `href`   : point d'entrée du module ;
 * - `routes` : préfixes d'URL qui appartiennent au module (état actif) ;
 * - `pages`  : sous-entrées affichées quand le module est ouvert.
 */
export const MODULES = [
  {
    id: 'incident', label: 'Incidents', description: 'Rétablir le service au plus vite', pillar: 'service', status: 'active',
    href: '/incidents', routes: ['/incidents'],
  },
  {
    id: 'request', label: 'Demandes', description: 'Demandes de service utilisateurs', pillar: 'service', status: 'active',
    href: '/requests', routes: ['/requests'],
  },
  {
    id: 'problem', label: 'Problèmes', description: 'Causes racines et erreurs connues', pillar: 'service', status: 'active',
    href: '/problems', routes: ['/problems', '/actions'],
    pages: [
      { href: '/problems', label: 'Registre des problèmes' },
      { href: '/actions', label: 'Actions correctives' },
    ],
  },
  {
    id: 'change', label: 'Changements', description: 'Changements et mises en production', pillar: 'agilite', status: 'active',
    href: '/changes', routes: ['/changes'],
    pages: [
      { href: '/changes', label: 'Registre des changements' },
      { href: '/changes/schedule', label: 'Calendrier des changements' },
    ],
  },
  {
    id: 'configuration', label: 'Configuration', description: 'Actifs et dépendances (CMDB)', pillar: 'service', status: 'active',
    href: '/configuration', routes: ['/configuration'],
  },
  {
    id: 'service-level', label: 'Niveaux de service', description: 'Catalogue, SLA et engagements', pillar: 'alignement', status: 'active',
    href: '/services', routes: ['/services', '/agreements'],
    pages: [
      { href: '/services', label: 'Catalogue des services' },
      { href: '/agreements', label: 'Accords (SLA)' },
    ],
  },
  {
    id: 'knowledge', label: 'Connaissances', description: 'Capitalisation et formation', pillar: 'leadership', status: 'active',
    href: '/knowledge', routes: ['/knowledge'],
  },
  {
    id: 'csi', label: 'Amélioration (CSI)', description: 'Amélioration continue des services', pillar: 'optimisation', status: 'active',
    href: '/improvements', routes: ['/improvements'],
  },
]

export const pillarOf = (id) => PILLARS.find((p) => p.id === id)
