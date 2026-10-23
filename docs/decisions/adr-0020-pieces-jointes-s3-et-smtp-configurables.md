# ADR-0020 — Pièces jointes sur un stockage compatible S3, courriels par un SMTP configurable

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-23
- **Statut** : accepté le 2026-10-23
- **Décideur** : Leipa

## Contexte

Le chantier F7 réunit les pièces jointes ([SOC-25](../reference/regles-metier.md#8-notifications))
et les notifications par courriel ([SOC-22](../reference/regles-metier.md#8-notifications)).
Deux questions restaient ouvertes : où ranger les fichiers (base, volume monté, stockage
objet) et comment joindre le serveur de messagerie de l'organisation.

## Décision

1. **Pièces jointes** dans un **stockage objet compatible S3**, Garage en référence
   (auto-hébergeable, léger, réplicable) ; tout service qui parle S3 convient (MinIO, Ceph,
   offre d'un hébergeur). La base ne garde que la description du fichier.
2. **Courriels** par un **serveur SMTP configurable** : celui de l'organisation ou un relais,
   sans chiffrement, en STARTTLS ou en TLS, avec ou sans authentification.
3. Les deux se règlent dans la **console d'administration** (Administration › Messagerie et
   stockage), pas dans les fichiers de configuration : adresse, compte, expéditeur, bucket,
   taille maximale, avec un **essai** de chacun. Les secrets (mot de passe SMTP, clé
   secrète S3) sont chiffrés par le coffre existant (`Discovery:VaultKey`), jamais renvoyés
   par l'API ; chaque modification est tracée au journal d'audit.
4. Désactivés par défaut : sans réglage, la plateforme fonctionne comme avant (notifications
   dans l'application, pas de pièce jointe).

## Conséquences

- Deux dépendances : MailKit (SMTP) et le SDK AWS pour S3, réglé pour les stockages
  compatibles (adressage par chemin, sommes de contrôle seulement quand l'opération les exige).
- Deux tables de réglages d'une ligne chacune ; la clé du coffre protège désormais aussi ces
  secrets : la perdre oblige à les ressaisir.
- L'exploitant fournit le stockage (un service Garage à côté de l'API, son bucket et sa
  clé) et un accès SMTP ; la page Santé dit si chacun est activé.
- La sauvegarde comprend désormais le bucket, en plus de la base.

## Alternatives envisagées

- **Fichiers en base** (colonne binaire) — écarté : sauvegardes et base gonflées, lecture
  plus lente, pas de réplication indépendante.
- **Volume monté** — écarté : lie l'API à un disque, gêne plusieurs instances et
  l'orchestrateur ; S3 fonctionne aussi bien en compose qu'en Kubernetes.
- **Réglages dans `appsettings.json` ou l'environnement** — écarté : chaque changement
  demanderait un redéploiement ; l'Admin doit pouvoir régler et tester lui-même.
