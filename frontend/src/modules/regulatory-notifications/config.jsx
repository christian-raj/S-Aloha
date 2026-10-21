import { api } from '../../api'
import { titleField, ownerField, dateTimeFr } from '../../core/fields'

const YES_NO = [{ value: 'true', label: 'Oui' }, { value: 'false', label: 'Non' }]

/** Prochaine échéance d'une déclaration : étape et date, en rouge si dépassée (DRG-11). */
export const nextDue = r => {
  if (!r.nextStep) return '—'
  const late = new Date(r.nextDueAt) < new Date()
  return <span className={late ? 'row-date is-late' : undefined}>{r.nextStep} · {dateTimeFr(r.nextDueAt)}</span>
}

// Déclarations réglementaires (DRG) : obligations de déclaration des incidents de sécurité
// (NIS 2, RGPD). S-Aloha calcule les échéances légales et garde la trace des envois faits
// sur le portail de l'autorité.
export const declarationConfig = {
  title: 'Déclarations réglementaires',
  sub: 'Déclarations des incidents de sécurité aux autorités (NIS 2 : ANSSI ; RGPD : CNIL) : échéances légales, contenu et envois.',
  api: api.regulatoryNotifications, basePath: '/regulatory-notifications', linkType: 'notification',
  // Créées par la qualification d'un incident de sécurité (INC-42), depuis sa fiche.
  noCreate: true,
  empty: 'Aucune déclaration : elles naissent de la qualification d’un incident de sécurité.', ownerLabel: 'Dont je suis responsable',
  statuses: [
    { value: 'À qualifier', tone: 'new' }, { value: 'À déclarer', tone: 'progress', manager: true },
    { value: 'Alerte précoce envoyée', tone: 'progress', manager: true },
    { value: 'Notification envoyée', tone: 'progress', manager: true },
    { value: 'Rapport final envoyé', tone: 'progress', manager: true },
    { value: 'Close', tone: 'done', manager: true }, { value: 'Non requise', tone: 'closed', manager: true },
  ],
  // Champs fixés à la création, renvoyés tels quels.
  keep: r => ({ incidentId: r.incidentId, regime: r.regime }),
  fields: [
    titleField('Titre'),
    ownerField('Responsable de la déclaration'),
    { key: '_delays', type: 'heading', label: 'Autorité et délais', hint: 'Délais légaux en heures calendaires, depuis la prise de connaissance (DRG-10).' },
    { key: 'authority', label: 'Autorité', managerOnly: true },
    { key: 'awareAt', label: 'Prise de connaissance', type: 'datetime', managerOnly: true },
    { key: 'awareAtReason', label: 'Motif du changement de la prise de connaissance', managerOnly: true, full: true,
      placeholder: 'Obligatoire si la prise de connaissance change : elle déplace les échéances.' },
    { key: '_content', type: 'heading', label: 'Contenu', hint: 'Complété par tous tant que la déclaration n’est pas close ; figé à chaque envoi (DRG-07).' },
    { key: 'summary', label: 'Résumé', type: 'textarea', rows: 3 },
    { key: 'impact', label: 'Impact (services, personnes, durée)', type: 'textarea', rows: 2 },
    { key: 'measuresTaken', label: 'Mesures prises', type: 'textarea', rows: 2 },
    { key: 'suspectedMalicious', label: 'Cause supposée malveillante (NIS 2)', type: 'select', boolean: true, options: YES_NO },
    { key: 'crossBorderImpact', label: 'Effets transfrontières (NIS 2)', type: 'select', boolean: true, options: YES_NO },
    { key: 'personalDataCategories', label: 'Catégories de données (RGPD)', placeholder: 'Ex. : identité, coordonnées, données bancaires' },
    { key: 'dataSubjectsCount', label: 'Nombre approximatif de personnes (RGPD)', type: 'number' },
    { key: 'lateReason', label: 'Motifs du retard (RGPD au-delà de 72 h)', type: 'textarea', rows: 2 },
    { key: '_sent', type: 'heading', label: 'Envois', hint: 'Date réelle de chaque envoi sur le portail de l’autorité (DRG-05) ; gestionnaires.' },
    { key: 'earlyWarningSentAt', label: 'Alerte précoce envoyée le (NIS 2)', type: 'datetime', managerOnly: true },
    { key: 'notificationSentAt', label: 'Notification envoyée le', type: 'datetime', managerOnly: true },
    { key: 'finalReportSentAt', label: 'Rapport final envoyé le (NIS 2)', type: 'datetime', managerOnly: true },
    { key: 'authorityCaseRef', label: 'Référence du dossier chez l’autorité', managerOnly: true },
    { key: 'notRequiredReason', label: 'Motif de non-déclaration', type: 'textarea', rows: 2, managerOnly: true,
      placeholder: 'Obligatoire pour « Non requise ».' },
  ],
  columns: [
    { h: 'Régime', v: r => r.regime },
    { h: 'Incident', v: r => r.incidentReference },
    { h: 'Autorité', v: r => r.authority },
    { h: 'Prochaine échéance', v: nextDue },
  ],
  meta: r => [`${r.regime} · ${r.authority}`, `Prise de connaissance le ${dateTimeFr(r.awareAt)}`],
  help: 'NIS 2 : alerte précoce sous 24 h, notification sous 72 h, rapport final un mois après la notification. RGPD : notification sous 72 h, motifs du retard au-delà. Chaque envoi se fait sur le portail de l’autorité ; S-Aloha en garde la date et le contenu envoyé.',
  linkHint: 'Problème (cause racine du rapport final), changements correctifs, évaluation NIS 2…',
}
