#!/usr/bin/env python3
"""Hygiène d'une pull request : trailers des commits et termes réservés.

    python3 scripts/check-hygiene.py <base> <head> [--strict]

- Un commit ne porte qu'un trailer : `Signed-off-by` (ADR-0010). Tout autre trailer
  (co-auteur, lien de session…) est signalé.
- Les termes réservés (noms, adresses internes… que le dépôt public ne doit pas contenir)
  sont lus dans la variable d'environnement HYGIENE_TERMES : une expression régulière par
  ligne, sans casse. Ils sont cherchés dans l'auteur, le message des commits et les lignes
  ajoutées. Un terme n'est jamais affiché : seulement son numéro.

Sortie : annotations GitHub `::warning` ; code 0, sauf avec --strict (code 1 si un
problème est trouvé).
"""
import os
import re
import subprocess
import sys

TRAILER = re.compile(r'^([A-Za-z][A-Za-z-]*): .+$')


def git(*args):
    return subprocess.run(['git', *args], check=True, capture_output=True, text=True).stdout


def termes():
    lignes = [l.strip() for l in os.environ.get('HYGIENE_TERMES', '').splitlines()]
    return [re.compile(l, re.I) for l in lignes if l and not l.startswith('#')]


def trouve(motifs, texte):
    return [i + 1 for i, m in enumerate(motifs) if m.search(texte)]


def main():
    base, head = sys.argv[1], sys.argv[2]
    strict = '--strict' in sys.argv
    motifs = termes()
    if not motifs:
        print('::notice::HYGIENE_TERMES absent (pull request d\'un fork ?) : seuls les trailers sont contrôlés.')
    problemes = 0

    for sha in git('rev-list', '--no-merges', f'{base}..{head}').split():
        court = sha[:7]
        message = git('log', '-1', '--format=%B', sha).rstrip().splitlines()
        bloc = []
        for ligne in reversed(message):
            if not ligne.strip():
                break
            bloc.append(ligne)
        for ligne in bloc:
            m = TRAILER.match(ligne)
            if m and m.group(1).lower() != 'signed-off-by':
                print(f'::warning::Commit {court} : trailer « {m.group(1)} » non admis (seul Signed-off-by l\'est).')
                problemes += 1
        identite = git('log', '-1', '--format=%an <%ae> / %cn <%ce>', sha)
        for n in trouve(motifs, identite):
            print(f'::warning::Commit {court} : auteur ou commiteur contenant le terme réservé n° {n}.')
            problemes += 1
        for n in trouve(motifs, '\n'.join(message)):
            print(f'::warning::Commit {court} : message contenant le terme réservé n° {n}.')
            problemes += 1

    fichier, numero = None, 0
    for ligne in git('diff', '--unified=0', '--no-color', f'{base}...{head}').splitlines():
        if ligne.startswith('+++ '):
            fichier = ligne[6:] if ligne.startswith('+++ b/') else None
        elif ligne.startswith('@@'):
            numero = int(re.search(r'\+(\d+)', ligne).group(1))
        elif ligne.startswith('+') and fichier:
            for n in trouve(motifs, ligne[1:]):
                print(f'::warning file={fichier},line={numero}::Terme réservé n° {n}.')
                problemes += 1
            numero += 1

    print(f'Hygiène : {problemes} problème(s).')
    return 1 if strict and problemes else 0


sys.exit(main())
