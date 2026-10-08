// Registre des pratiques ITIL couvertes par S-Aloha, et des sections de
// paramétrage et d'administration — source unique de la navigation (barre
// latérale : Pilotage, Pratiques, Paramétrage, Administration). Un module passe de
// `soon` à `active` le jour où son interface existe : rien d'autre à toucher
// côté navigation. Cartographie et état : docs/reference/produit.md.

/**
 * Pratiques ITIL, dans l'ordre du cycle de vie du service.
 * - `href`   : point d'entrée du module ;
 * - `routes` : préfixes d'URL qui appartiennent au module (état actif) ;
 * - `pages`  : sous-entrées affichées quand le module est ouvert ; un `href`
 *              peut porter un filtre (`/problems?status=…`) ;
 * - `roles`  : (module ou sous-entrée) rôles qui voient l'entrée — absent, tous.
 *              Réservé au paramétrage d'une pratique (Manager, Admin) : l'entrée
 *              est masquée pour les autres rôles, que l'API refuse de toute façon.
 *
 * Une entrée n'apparaît que lorsque sa règle est implémentée : pas de page
 * vide (docs/reference/frontend.md § Navigation cible).
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
      { href: '/problems?' + new URLSearchParams({ status: 'Erreur connue' }), label: 'Erreurs connues' },
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
  {
    // Gestion de la sécurité de l'information (ITIL 4) : évaluation NIS 2.
    id: 'compliance', label: 'Conformité NIS 2', description: 'Évaluations ReCyF (ANSSI)', status: 'active',
    href: '/assessments', routes: ['/assessments'],
  },
]

/**
 * Sections réservées, après les pratiques. Une section n'apparaît que si elle
 * contient au moins une entrée visible pour le rôle : vides aujourd'hui, elles
 * restent invisibles jusqu'à leur première page (pas de section vide).
 * - Paramétrage : le fonctionnement des pratiques (catalogue de demandes,
 *   modèles de changement, périodes de gel, référentiels) — délégable aux
 *   gestionnaires ;
 * - Administration : la plateforme elle-même (utilisateurs et rôles, santé,
 *   journal d'audit) — Admin seul.
 * Une entrée : { href, label, description, icon (clé de SECTION_ICONS), roles? }.
 */
export const SECTIONS = [
  {
    id: 'settings', label: 'Paramétrage', roles: ['Manager', 'Admin'], entries: [
      { href: '/settings/nis2', label: 'Référentiel NIS 2', description: 'Structure et texte des exigences',
        icon: 'settings', roles: ['Admin'] },
    ]
  },
  { id: 'administration', label: 'Administration', roles: ['Admin'], entries: [] },
]

/** L'entrée (module, sous-entrée, section) est-elle visible pour ce rôle ? */
export const visibleFor = (entry, role) => !entry.roles || entry.roles.includes(role)
