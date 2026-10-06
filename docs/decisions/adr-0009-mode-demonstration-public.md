# ADR-0009 — Un mode démonstration public, avec son propre annuaire Active Directory

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

S-Aloha délègue toute l'identité à l'Active Directory de l'organisation : aucun compte
local. C'est un atout en production, mais un mur pour qui veut **essayer** le projet : un
`docker compose up` sans AD mène à un écran de connexion inutilisable. Un développeur
qui découvre le dépôt, un sponsor qui l'évalue ou un relecteur d'une pull request ne
peuvent rien voir. C'est le premier frein relevé par la
[stratégie communauté](../strategie-communaute.md) (décision D1).

## Décision

Le dépôt fournit un **mode démonstration** :

- `demo/annuaire/` : image d'un contrôleur de domaine **Samba AD** (domaine `SALOHA.LAN`)
  qui crée au premier démarrage les groupes S-Aloha, le compte de service et trois
  comptes de démonstration, un par rôle (`demo.admin`, `demo.manager`, `demo.user`) ;
- `docker-compose.demo.yml` : fichier superposé au compose principal, qui ajoute
  l'annuaire et y branche l'API :

  ```bash
  docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build
  ```

Les mots de passe de démonstration sont **publics**, écrits dans le fichier et le README.

## Conséquences

- On essaie S-Aloha en cinq minutes, avec Docker seul, sur un vrai annuaire AD (schéma,
  `sAMAccountName`, `memberOf`) : le code d'authentification testé est celui de la
  production, sans chemin de contournement.
- Le conteneur de l'annuaire est **privilégié** (Samba AD a besoin des attributs étendus
  du système de fichiers) : acceptable pour un poste d'évaluation, jamais en production.
- Le mode démonstration n'est **jamais** une configuration de production : mots de passe
  publics, LDAP sans TLS. L'avertissement figure dans le compose, le README et
  [exploitation § Mode démonstration](../reference/exploitation.md#mode-démonstration).
- Les données d'exemple (problèmes, incidents…) restent à ajouter : l'application démarre
  vide.

## Alternatives envisagées

- **Authentification locale de secours** (comptes en base, mode « dev ») — écarté : un
  second chemin d'authentification à maintenir et à sécuriser, et un essai qui ne montre
  pas ce qui fait la valeur du produit (l'intégration AD).
- **Serveur LDAP générique** (OpenLDAP, lldap) — écarté : ni `sAMAccountName` ni
  `memberOf` tels que l'AD les expose ; on testerait un autre comportement que celui de
  la production.
- **Démo en ligne seulement** — insuffisant : elle ne sert pas un contributeur qui doit
  lancer le code qu'il modifie. Elle reste prévue en complément (C8).
