import React from 'react'

// Jeu d'icônes maison — une seule grammaire : 24×24, trait 1,7, bouts
// arrondis, couleur héritée (currentColor). Pas de bibliothèque d'icônes :
// une dizaine de tracés ne justifie pas une dépendance.

const Svg = ({ size = 18, children, ...rest }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden {...rest}>
    {children}
  </svg>
)

/** Marque S-Aloha : un « S » tracé, posé sur une ligne d'horizon — « Aloha »,
 * c'est ce qui passe avant (« Service alohan'ny zavatra rehetra »). */
export const BrandMark = ({ size = 20 }) => (
  <Svg size={size} strokeWidth="1.9">
    <path d="M16 7.2c-.8-1.2-2.3-1.9-4-1.9-2.3 0-4 1.2-4 3 0 4.1 8 2.5 8 6.6 0 1.9-1.8 3-4.1 3-1.8 0-3.3-.7-4.1-2" />
    <path d="M4 21h16" opacity=".55" />
  </Svg>
)

export const IconConsole = (p) => <Svg {...p}><rect x="3.5" y="3.5" width="7" height="7" /><rect x="13.5" y="3.5" width="7" height="7" /><rect x="3.5" y="13.5" width="7" height="7" /><rect x="13.5" y="13.5" width="7" height="7" /></Svg>
export const IconReport = (p) => <Svg {...p}><path d="M4 20V10M10 20V4M16 20v-7M22 20H2" /></Svg>
export const IconIncident = (p) => <Svg {...p}><path d="M12 3 2.5 20h19L12 3Z" /><path d="M12 10v4M12 17v.01" /></Svg>
export const IconRequest = (p) => <Svg {...p}><path d="M4 5h16v11H8l-4 4V5Z" /><path d="M8 10h8" /></Svg>
export const IconProblem = (p) => <Svg {...p}><circle cx="11" cy="11" r="6.5" /><path d="m16 16 5 5M11 8v3l2 1.5" /></Svg>
export const IconChange = (p) => <Svg {...p}><path d="M4 8h13l-3-3M20 16H7l3 3" /></Svg>
export const IconConfig = (p) => <Svg {...p}><circle cx="6" cy="6" r="2.5" /><circle cx="18" cy="6" r="2.5" /><circle cx="12" cy="18" r="2.5" /><path d="M7.6 8 11 15.8M16.4 8 13 15.8M8.5 6h7" /></Svg>
export const IconSla = (p) => <Svg {...p}><circle cx="12" cy="13" r="8" /><path d="M12 9v4l2.5 2.5M9 2.5h6" /></Svg>
export const IconKnowledge = (p) => <Svg {...p}><path d="M4 5.5C6.5 4 9.5 4 12 6c2.5-2 5.5-2 8-.5V19c-2.5-1.5-5.5-1.5-8 .5-2.5-2-5.5-2-8-.5V5.5Z" /><path d="M12 6v13.5" /></Svg>
export const IconCsi = (p) => <Svg {...p}><path d="M20 12a8 8 0 1 1-2.3-5.7M20 4v4h-4" /></Svg>
export const IconSettings = (p) => <Svg {...p}><path d="M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0" /><circle cx="16" cy="6" r="2" /><circle cx="10" cy="12" r="2" /><circle cx="18" cy="18" r="2" /></Svg>
export const IconShield = (p) => <Svg {...p}><path d="M12 3 4.5 6v5.5c0 4.6 3.1 8 7.5 9.5 4.4-1.5 7.5-4.9 7.5-9.5V6L12 3Z" /></Svg>
export const IconLogout = (p) => <Svg size={15} {...p}><path d="M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10" /></Svg>
export const IconMenu = (p) => <Svg {...p}><path d="M4 6h16M4 12h16M4 18h16" /></Svg>
export const IconClose = (p) => <Svg {...p}><path d="M6 6l12 12M18 6 6 18" /></Svg>
export const IconSun = (p) => <Svg size={15} {...p}><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></Svg>
export const IconMoon = (p) => <Svg size={15} {...p}><path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" /></Svg>

/** Icônes des entrées de section (Paramétrage, Administration) du registre. */
export const SECTION_ICONS = { settings: IconSettings, admin: IconShield }

/** Icône de chaque processus du registre (modules/registry.js). */
export const MODULE_ICONS = {
  incident: IconIncident,
  request: IconRequest,
  problem: IconProblem,
  change: IconChange,
  configuration: IconConfig,
  'service-level': IconSla,
  knowledge: IconKnowledge,
  csi: IconCsi,
}
