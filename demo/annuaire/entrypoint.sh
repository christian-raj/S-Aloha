#!/usr/bin/env bash
# Provisionne le domaine au premier démarrage (volume /data vide), puis crée
# les groupes S-Aloha, le compte de service et les comptes de démo s'ils
# manquent. Idempotent : un redémarrage ne touche à rien d'existant.
#
# Variables : AD_REALM, AD_DOMAIN, AD_ADMIN_PASSWORD, SVC_PASSWORD,
#             DEMO_ADMIN_PASSWORD, DEMO_MANAGER_PASSWORD, DEMO_USER_PASSWORD
set -euo pipefail

CONF=/data/etc/smb.conf

if [ ! -f /data/private/sam.ldb ]; then
  echo ">> Provisionnement du domaine $AD_REALM"
  samba-tool domain provision \
    --targetdir=/data \
    --realm="$AD_REALM" --domain="$AD_DOMAIN" \
    --server-role=dc --dns-backend=SAMBA_INTERNAL \
    --adminpass="$AD_ADMIN_PASSWORD"
fi

# Liaison simple sans TLS autorisée : la configuration d'évaluation de S-Aloha
# interroge l'annuaire en LDAP 389 (voir docs/reference/securite.md). Le
# provisionnement n'écrit pas cette option : on la pose dans [global].
# Les mises à jour DNS dynamiques sont coupées : le DNS interne du DC contient
# déjà ses enregistrements, et personne d'autre ne l'interroge.
grep -q 'ldap server require strong auth' "$CONF" || sed -i '/^\[global\]/a\
\tldap server require strong auth = no\
\tdns update command = /bin/true' "$CONF"

st() { samba-tool "$@" -s "$CONF" -H /data/private/sam.ldb; }

ensure_group() {
  st group show "$1" >/dev/null 2>&1 || st group add "$1"
}

# ensure_user <compte> <prénom> <nom> <mot de passe> [groupe]
ensure_user() {
  if ! st user show "$1" >/dev/null 2>&1; then
    # CN = nom de compte, pour un DN prévisible (CN=svc-saloha,CN=Users,…).
    st user create "$1" "$4" --given-name="$2" --surname="$3" --use-username-as-cn
    st user setexpiry "$1" --noexpiry
  fi
  if [ -n "${5:-}" ]; then
    st group addmembers "$5" "$1" >/dev/null 2>&1 || true
  fi
}

ensure_group GRP-SALOHA-ADMINS
ensure_group GRP-SALOHA-MANAGERS
ensure_group GRP-SALOHA-USERS

ensure_user svc-saloha  Service S-Aloha "$SVC_PASSWORD"
ensure_user demo.admin   Alice  Admin    "$DEMO_ADMIN_PASSWORD"   GRP-SALOHA-ADMINS
ensure_user demo.manager Marc   Manager  "$DEMO_MANAGER_PASSWORD" GRP-SALOHA-MANAGERS
ensure_user demo.user    Ursula Utilisatrice "$DEMO_USER_PASSWORD" GRP-SALOHA-USERS

echo ">> Annuaire prêt"
exec samba -i -s "$CONF" --debug-stdout
