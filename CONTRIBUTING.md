# Contribuer à S-Aloha

Merci de votre intérêt. S-Aloha est distribué sous [AGPLv3](LICENSE) : toute
contribution est publiée sous la même licence.

## Signaler un bug ou proposer une évolution

Les [issues](https://github.com/christian-raj/S-Aloha/issues) sont la source de vérité
du projet ([ADR-0001](docs/decisions/adr-0001-github-issues-source-de-verite.md)).
Utilisez les modèles proposés. Une faille de sécurité ne passe **jamais** par une
issue publique : voir [SECURITY.md](SECURITY.md).

## Proposer une modification

1. Une pull request par sujet, à partir de `main`.
2. Lancez les contrôles avant de pousser :

   ```bash
   TESTCONTAINERS_RYUK_DISABLED=true dotnet test tests/SAloha.Api.Tests   # API, Docker requis
   cd frontend && npm test && npm run build                               # interface
   python3 scripts/check-docs.py                                          # documentation
   ```

3. La documentation est mise à jour dans la même pull request que le code
   ([ADR-0002](docs/decisions/adr-0002-documentation-dans-le-depot.md)), en suivant le
   [protocole documentaire](docs/protocole-documentation.md).
4. La CI doit être au vert pour que la pull request soit fusionnée.

Messages de commit en français, à l'indicatif, décrivant le changement.
