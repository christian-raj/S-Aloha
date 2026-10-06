// Registre des processus ITIL couverts par S-Aloha — source unique de la
// navigation (barre latérale). Un module passe de
// `soon` à `active` le jour où son interface existe : rien d'autre à toucher
// côté navigation. Cartographie et état : docs/reference/produit.md.

/**
 * Processus ITIL, dans l'ordre du cycle de vie du service.
 * - `href`   : point d'entrée du module ;
 * - `routes` : préfixes d'URL qui appartiennent au module (état actif) ;
 * - `pages`  : sous-entrées affichées quand le module est ouvert.
 */
export const MODULES = [
  {
    id: 'incident', label: 'Incidents', description: 'Rétablir le service au plus vite', status: 'active',
    href: '/incidents', routes: ['/incidents'],
  },
  {
    id: 'request', label: 'Demandes', description: 'Demandes de service utilisateurs', status: 'active',
    href: '/requests', routes: ['/requests'],
  },
  {
    id: 'problem', label: 'Problèmes', description: 'Causes racines et erreurs connues', status: 'active',
    href: '/problems', routes: ['/problems', '/actions'],
    pages: [
      { href: '/problems', label: 'Registre des problèmes' },
      { href: '/actions', label: 'Actions correctives' },
    ],
  },
  {
    id: 'change', label: 'Changements', description: 'Changements et mises en production', status: 'active',
    href: '/changes', routes: ['/changes'],
    pages: [
      { href: '/changes', label: 'Registre des changements' },
      { href: '/changes/schedule', label: 'Calendrier des changements' },
    ],
  },
  {
    id: 'configuration', label: 'Configuration', description: 'Actifs et dépendances (CMDB)', status: 'active',
    href: '/configuration', routes: ['/configuration'],
  },
  {
    id: 'service-level', label: 'Niveaux de service', description: 'Catalogue, SLA et engagements', status: 'active',
    href: '/services', routes: ['/services', '/agreements'],
    pages: [
      { href: '/services', label: 'Catalogue des services' },
      { href: '/agreements', label: 'Accords (SLA)' },
    ],
  },
  {
    id: 'knowledge', label: 'Connaissances', description: 'Capitalisation et formation', status: 'active',
    href: '/knowledge', routes: ['/knowledge'],
  },
  {
    id: 'csi', label: 'Amélioration (CSI)', description: 'Amélioration continue des services', status: 'active',
    href: '/improvements', routes: ['/improvements'],
  },
]

