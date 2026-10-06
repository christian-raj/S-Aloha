#!/usr/bin/env python3
"""Charge un jeu de données de démonstration dans S-Aloha, par l'API.

Passer par l'API (et non par la base) garantit que les données respectent les mêmes
règles que celles saisies dans l'interface : références, priorités, droits, statuts.
Toutes les données sont fictives.

Lancé par le service `donnees` de docker-compose.demo.yml ; utilisable seul :

    python3 demo/donnees/charger.py http://localhost

Idempotent : ne fait rien si des problèmes existent déjà.
"""
import json
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

BASE = (sys.argv[1] if len(sys.argv) > 1 else 'http://api:8080').rstrip('/')
COMPTES = {
    'admin': ('demo.admin', 'Demo-Admin-2026'),
    'manager': ('demo.manager', 'Demo-Manager-2026'),
    'user': ('demo.user', 'Demo-User-2026'),
}
MARC = {'ownerType': 'User', 'ownerId': 'demo.manager', 'ownerDisplayName': 'Marc Manager'}
URSULA = {'ownerType': 'User', 'ownerId': 'demo.user', 'ownerDisplayName': 'Ursula Utilisatrice'}
MAINTENANT = datetime.now(timezone.utc).replace(microsecond=0)


def jour(delta):
    return (MAINTENANT + timedelta(days=delta)).isoformat().replace('+00:00', 'Z')


def appel(methode, chemin, jeton=None, corps=None):
    req = urllib.request.Request(BASE + chemin, method=methode,
                                 data=json.dumps(corps).encode() if corps is not None else None)
    req.add_header('Content-Type', 'application/json')
    if jeton:
        req.add_header('Authorization', f'Bearer {jeton}')
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            texte = r.read().decode()
            return json.loads(texte) if texte else None
    except urllib.error.HTTPError as e:
        sys.exit(f'✖ {methode} {chemin} → {e.code} {e.read().decode()[:300]}')


def connexion(role):
    nom, mdp = COMPTES[role]
    for _ in range(60):  # l'API et l'annuaire peuvent encore démarrer
        try:
            return appel('POST', '/api/auth/login', corps={'username': nom, 'password': mdp})['token']
        except (urllib.error.URLError, ConnectionError, OSError):
            time.sleep(5)
    sys.exit('✖ API injoignable')


def creer(jeton, chemin, corps, statut=None):
    """Crée un enregistrement, puis lui donne son statut (transition contrôlée par l'API)."""
    e = appel('POST', chemin, jeton, corps)
    if statut:
        e = appel('PUT', f'{chemin}/{e["id"]}', jeton, {**corps, 'status': statut})
    return e


def lier(jeton, type_, id_, reference):
    appel('POST', '/api/links', jeton, {'fromType': type_, 'fromId': id_, 'toReference': reference})


def main():
    m, u = connexion('manager'), connexion('user')
    if appel('GET', '/api/problems', m):
        print('Données déjà présentes : rien à faire.')
        return

    # --- Catalogue des services et niveaux de service ---------------------------
    messagerie = creer(m, '/api/services', {**MARC, 'title': 'Messagerie',
        'description': 'Courriel et calendriers de toute l’organisation.',
        'criticality': 'Élevée', 'serviceHours': '24 h/24, 7 j/7'}, 'En service')
    vpn = creer(m, '/api/services', {**MARC, 'title': 'Accès distant (VPN)',
        'description': 'Connexion sécurisée au réseau interne depuis l’extérieur.',
        'criticality': 'Élevée', 'serviceHours': 'Jours ouvrés, 7 h – 20 h'}, 'En service')
    erp = creer(m, '/api/services', {**MARC, 'title': 'ERP Finances',
        'description': 'Comptabilité, achats et paie.',
        'criticality': 'Moyenne', 'serviceHours': 'Jours ouvrés, 8 h – 18 h'}, 'En service')
    creer(m, '/api/agreements', {**MARC, 'title': 'SLA Accès distant — Directions régionales',
        'description': 'Engagement de disponibilité et de résolution du VPN.',
        'serviceId': vpn['id'], 'customer': 'Directions régionales', 'availabilityTarget': 99.5,
        'resolutionHoursP1': 4, 'resolutionHoursP2': 8, 'resolutionHoursP3': 24, 'resolutionHoursP4': 72,
        'validFrom': jour(-90), 'validTo': jour(275), 'reviewDate': jour(-3)}, 'En vigueur')

    # --- Configuration (CMDB) ----------------------------------------------------
    def ci(titre, type_, desc):
        return creer(m, '/api/configuration-items', {**MARC, 'title': titre, 'description': desc,
            'ciType': type_, 'environment': 'Production', 'location': 'Salle serveurs — siège'})
    passerelle = ci('VPN-GW-01', 'Réseau', 'Passerelle VPN principale.')
    parefeu = ci('FW-SIEGE-01', 'Réseau', 'Pare-feu périmétrique du siège.')
    mail = ci('SRV-MAIL-01', 'Serveur', 'Serveur de messagerie.')
    bdd = ci('BDD-ERP-01', 'Base de données', 'Base PostgreSQL de l’ERP.')
    for source, cible, type_ in [(passerelle, parefeu, 'Dépend de'), (mail, parefeu, 'Dépend de'),
                                 (bdd, parefeu, 'Se connecte à')]:
        appel('POST', f'/api/configuration-items/{source["id"]}/relations', m,
              {'targetReference': cible['reference'], 'type': type_})
    lier(m, 'ci', passerelle['id'], vpn['reference'])
    lier(m, 'ci', mail['id'], messagerie['reference'])
    lier(m, 'ci', bdd['id'], erp['reference'])

    # --- Incidents ---------------------------------------------------------------
    inc_vpn = creer(u, '/api/incidents', {**URSULA, 'title': 'VPN inaccessible pour les directions régionales',
        'description': 'Depuis 8 h, aucune connexion VPN n’aboutit hors du siège.',
        'impact': 'Élevé', 'urgency': 'Élevée', 'category': 'Réseau',
        'affectedService': 'Accès distant (VPN)', 'isMajor': True, 'resolution': None}, 'En cours')
    lier(u, 'incident', inc_vpn['id'], passerelle['reference'])
    inc_vpn2 = creer(u, '/api/incidents', {**URSULA, 'title': 'Coupures VPN toutes les 20 minutes',
        'description': 'Les sessions VPN tombent régulièrement à Toamasina.',
        'impact': 'Moyen', 'urgency': 'Moyenne', 'category': 'Réseau',
        'affectedService': 'Accès distant (VPN)', 'isMajor': False,
        'resolution': 'Redémarrage de la passerelle : contournement en attendant l’analyse.'}, 'Résolu')
    creer(u, '/api/incidents', {**URSULA, 'title': 'Messagerie lente le matin',
        'description': 'Ouverture des boîtes aux lettres très lente entre 8 h et 9 h.',
        'impact': 'Faible', 'urgency': 'Moyenne', 'category': 'Messagerie',
        'affectedService': 'Messagerie', 'isMajor': False, 'resolution': None})

    # --- Problèmes, analyse, actions ---------------------------------------------
    def probleme(titre, desc, impact, urgence, categorie, service):
        return appel('POST', '/api/problems', u, {'title': titre, 'description': desc, 'impact': impact,
            'urgency': urgence, 'category': categorie, 'affectedService': service,
            'status': None, 'knownErrorWorkaround': None, 'rootCause': None})

    p_vpn = probleme('Coupures récurrentes du VPN',
        'Plusieurs incidents de coupure VPN depuis la mise à jour du mois dernier.',
        'Élevé', 'Moyenne', 'Réseau', 'Accès distant (VPN)')
    appel('POST', f'/api/problems/{p_vpn["id"]}/analyses', u, {'method': 'FIVE_WHYS',
        'dataJson': json.dumps({'problem': 'Les sessions VPN tombent toutes les 20 minutes',
            'whys': ['La passerelle renégocie les clés de session',
                     'La durée de vie des clés est passée de 8 h à 20 min',
                     'La mise à jour du firmware a réinitialisé la configuration',
                     'La configuration n’était pas sauvegardée avant la mise à jour',
                     'Aucune procédure de changement ne l’exigeait'],
            'rootCause': 'Mise à jour du firmware sans sauvegarde préalable de la configuration'},
            ensure_ascii=False),
        'conclusion': 'Mise à jour du firmware sans sauvegarde préalable de la configuration'})
    appel('PUT', f'/api/problems/{p_vpn["id"]}', m, {'title': p_vpn['title'],
        'description': p_vpn['description'], 'status': 'Erreur connue', 'impact': 'Élevé',
        'urgency': 'Moyenne', 'category': 'Réseau', 'affectedService': 'Accès distant (VPN)',
        'knownErrorWorkaround': 'Redémarrer la passerelle VPN-GW-01 ; les sessions tiennent ensuite plusieurs heures.',
        'rootCause': 'Mise à jour du firmware sans sauvegarde préalable de la configuration'})
    raci = [{'role': 'R', 'assigneeType': 'User', 'assigneeId': 'demo.user', 'assigneeDisplayName': 'Ursula Utilisatrice'},
            {'role': 'A', 'assigneeType': 'User', 'assigneeId': 'demo.manager', 'assigneeDisplayName': 'Marc Manager'},
            {'role': 'I', 'assigneeType': 'Group', 'assigneeId': 'GRP-SALOHA-USERS', 'assigneeDisplayName': 'GRP-SALOHA-USERS'}]
    appel('POST', f'/api/problems/{p_vpn["id"]}/actions', u, {'title': 'Restaurer la durée de vie des clés à 8 h',
        'description': 'Corriger la configuration de VPN-GW-01.', 'status': None, 'dueDate': jour(-2), 'raci': raci})
    appel('POST', f'/api/problems/{p_vpn["id"]}/actions', u, {'title': 'Sauvegarder la configuration avant toute mise à jour',
        'description': 'Ajouter l’étape au modèle de changement des équipements réseau.', 'status': None,
        'dueDate': jour(10), 'raci': raci})
    lier(u, 'problem', p_vpn['id'], inc_vpn['reference'])
    lier(u, 'problem', p_vpn['id'], inc_vpn2['reference'])
    lier(u, 'problem', p_vpn['id'], passerelle['reference'])

    probleme('Échecs intermittents des sauvegardes nocturnes',
        'La sauvegarde de BDD-ERP-01 échoue une nuit sur trois.', 'Moyen', 'Moyenne',
        'Stockage', 'ERP Finances')

    # --- Changement lié au problème ----------------------------------------------
    chg = {**MARC, 'title': 'Corriger la configuration de la passerelle VPN',
        'description': 'Restaurer la durée de vie des clés et sauvegarder la configuration.',
        'changeType': 'Normal', 'risk': 'Moyen', 'plannedStart': jour(5), 'plannedEnd': jour(5),
        'implementationPlan': '1. Sauvegarder la configuration\n2. Corriger la durée de vie des clés\n3. Tester depuis une direction régionale',
        'backoutPlan': 'Restaurer la configuration sauvegardée.', 'outcome': None}
    changement = creer(m, '/api/changes', chg, 'Autorisé')
    appel('PUT', f'/api/changes/{changement["id"]}', m, {**chg, 'status': 'Planifié'})
    lier(m, 'change', changement['id'], p_vpn['reference'])
    lier(m, 'change', changement['id'], passerelle['reference'])
    creer(u, '/api/changes', {**URSULA, 'title': 'Ajout d’une boîte aux lettres partagée « Achats »',
        'description': 'Changement standard du catalogue.', 'changeType': 'Standard', 'risk': 'Faible',
        'plannedStart': jour(2), 'plannedEnd': jour(2), 'implementationPlan': 'Procédure standard.',
        'backoutPlan': 'Supprimer la boîte.', 'outcome': None})

    # --- Demandes ----------------------------------------------------------------
    creer(u, '/api/requests', {**URSULA, 'title': 'Accès VPN pour une nouvelle arrivante',
        'description': 'Prise de poste lundi à la direction régionale de Mahajanga.',
        'requestedItem': 'Compte VPN', 'requestedFor': 'demo.user',
        'requestedForDisplayName': 'Ursula Utilisatrice', 'dueDate': jour(4)})
    creer(m, '/api/requests', {**URSULA, 'title': 'Ordinateur portable pour le service Achats',
        'description': 'Remplacement d’un poste en panne.', 'requestedItem': 'Ordinateur portable',
        'requestedFor': 'demo.user', 'requestedForDisplayName': 'Ursula Utilisatrice',
        'dueDate': jour(7)}, 'Approuvée')

    # --- Connaissances -----------------------------------------------------------
    article = creer(m, '/api/knowledge', {**MARC, 'title': 'Rétablir une session VPN qui coupe',
        'description': 'Contournement de l’erreur connue sur VPN-GW-01.', 'articleType': 'Erreur connue',
        'content': 'Symptôme : la session VPN tombe toutes les 20 minutes.\n\nContournement : redémarrer la passerelle VPN-GW-01 depuis la console d’administration, puis demander aux utilisateurs de se reconnecter.\n\nCorrection définitive : changement planifié.',
        'keywords': 'vpn, coupure, passerelle', 'reviewDate': jour(60)}, 'Publié')
    lier(m, 'article', article['id'], p_vpn['reference'])

    # --- Amélioration continue ---------------------------------------------------
    creer(m, '/api/improvements', {**MARC, 'title': 'Réduire le délai de résolution des incidents P2',
        'description': 'Les incidents P2 dépassent souvent la cible de 8 h.', 'step': 2, 'priority': 'Élevée',
        'benefit': 'Respect du SLA Accès distant', 'baseline': '11 h en moyenne', 'target': '6 h',
        'outcome': None, 'dueDate': jour(45)}, 'Validée')

    print('✔ Données de démonstration chargées.')


if __name__ == '__main__':
    main()
