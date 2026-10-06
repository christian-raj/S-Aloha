# ADR-0010 — Les contributions sont signées sous le Developer Certificate of Origin, sans cession de droits

<sub>[← Documentation](../readme.md) · [Toutes les décisions](readme.md)</sub>

- **Date** : 2026-10-06
- **Statut** : accepté
- **Décideur** : Christian Rajaonary

## Contexte

Le projet s'ouvre aux contributions extérieures. Il faut décider, **avant la première
d'entre elles**, du cadre juridique des contributions : ce choix ne se rattrape pas, car
changer de licence ensuite demande l'accord de chaque contributeur
([ADR-0006](adr-0006-licence-agplv3.md)). Deux modèles existent :

- **DCO** (*Developer Certificate of Origin*) : le contributeur certifie, par une ligne
  `Signed-off-by` dans chaque commit, qu'il a le droit de soumettre son code sous la
  licence du projet. Il reste titulaire de ses droits.
- **CLA** (*Contributor License Agreement*) : le contributeur signe un accord qui cède ou
  concède largement ses droits au mainteneur, ce qui permet notamment une **double
  licence** commerciale.

## Décision

S-Aloha adopte le **DCO 1.1**. Chaque commit porte `Signed-off-by` (`git commit -s`).
Aucun CLA, aucune cession de droits. Le modèle reste **100 % AGPLv3**.

## Conséquences

- Contribuer ne demande aucune démarche juridique : une option de `git commit`.
  Le [guide de contribution](../../CONTRIBUTING.md) et le modèle de pull request le
  rappellent.
- Le projet **renonce à une double licence commerciale** : le code des contributeurs
  extérieurs ne pourra être distribué que sous AGPLv3 (ou version ultérieure). Le
  financement repose donc sur le sponsoring et les services
  ([ADR-0011](adr-0011-financement-sponsoring-et-services.md)), pas sur la vente de
  licences.
- Vérification : à la relecture de chaque pull request ; une vérification automatique
  (application DCO de GitHub ou contrôle de CI) pourra être ajoutée.

## Alternatives envisagées

- **CLA** — écarté : freine les contributions (signature préalable, méfiance envers une
  cession de droits) et ne sert qu'un modèle de double licence que le projet ne retient
  pas.
- **Aucun cadre** — écarté : rien n'atteste que le contributeur avait le droit de
  soumettre son code (code d'employeur, code copié).
