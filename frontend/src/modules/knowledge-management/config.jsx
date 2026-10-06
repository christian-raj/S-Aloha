import { api } from '../../api'
import { titleField, descriptionField, ownerField, dateFr, dateTimeFr } from '../../core/fields'

export const ARTICLE_TYPES = ['Solution', 'Procédure', 'Erreur connue', 'FAQ']

// Gestion des connaissances (ITIL 4) : maintenir et améliorer l'usage
// efficace, efficient et pratique de l'information et des connaissances.
export const knowledgeConfig = {
  title: 'Connaissances',
  sub: 'Solutions, procédures, erreurs connues et FAQ : capitaliser pour résoudre plus vite et former (ITIL 4).',
  api: api.knowledge, basePath: '/knowledge', linkType: 'article',
  createLabel: '+ Rédiger un article', createTitle: 'Nouvel article',
  empty: 'Aucun article dans la base de connaissances.', ownerLabel: 'Dont je suis responsable',
  searchPlaceholder: 'Rechercher (titre, mots-clés, contenu)…',
  filter: { key: 'type', label: 'Tous les types', options: ARTICLE_TYPES },
  statuses: [
    { value: 'Brouillon', tone: 'new' }, { value: 'Publié', tone: 'done', manager: true }, { value: 'Archivé', tone: 'closed' },
  ],
  fields: [
    titleField('Titre', 'Ex. : Réinitialiser l\'accès VPN d\'un utilisateur'),
    { key: 'articleType', label: 'Type', type: 'select', options: ARTICLE_TYPES, required: true, default: 'Solution' },
    { key: 'keywords', label: 'Mots-clés', placeholder: 'vpn, accès, nomade' },
    { key: 'reviewDate', label: 'Prochaine revue', type: 'date' },
    ownerField('Responsable de l\'article'),
    descriptionField('Résumé', 'En une ou deux phrases : à quoi sert cet article', 2),
    { key: 'content', label: 'Contenu', type: 'textarea', rows: 12, placeholder: 'Contexte, étapes, vérifications…' },
  ],
  columns: [
    { h: 'Type', v: r => r.articleType },
    { h: 'Mots-clés', v: r => r.keywords || '—' },
    { h: 'Revue', v: r => dateFr(r.reviewDate) },
  ],
  meta: r => [r.publishedAt && `Publié par ${r.publishedBy} le ${dateTimeFr(r.publishedAt)}`],
  help: 'La publication est validée par un gestionnaire ; un article publié doit avoir un contenu. Retouché par un autre utilisateur, il repasse en brouillon.',
  linkHint: 'Problème (erreur connue), incidents résolus grâce à l\'article…',
}
