# Politique de sécurité

## Signaler une vulnérabilité

**N'ouvrez pas d'issue publique.** Utilisez le signalement privé de GitHub :
onglet **Security** → **Report a vulnerability**
([lien direct](https://github.com/christian-raj/S-Aloha/security/advisories/new)).

Indiquez la version ou le commit concerné, les étapes pour reproduire et l'impact
estimé. Vous recevrez un accusé de réception sous 7 jours ; le correctif et l'avis
de sécurité sont publiés ensemble, avec votre accord pour la mention de votre nom.

## Versions suivies

Seule la branche `main` reçoit des correctifs de sécurité.

## Avant une mise en production

La configuration fournie sert à l'évaluation (secrets d'exemple, ni LDAPS ni HTTPS).
Suivez la [liste de durcissement](docs/reference/securite.md#points-de-durcissement-avant-production).
