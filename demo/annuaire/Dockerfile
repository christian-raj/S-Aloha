# Contrôleur de domaine Samba AD de démonstration : un vrai annuaire Active Directory
# (schéma AD, sAMAccountName, memberOf) pour essayer S-Aloha sans le SI d'une
# organisation. Jamais en production : on branche alors l'AD existant.
FROM docker.io/library/debian:bookworm-slim
RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      samba samba-ad-dc samba-ad-provision winbind krb5-user ldb-tools \
 && rm -rf /var/lib/apt/lists/* /etc/samba/smb.conf
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 389
ENTRYPOINT ["/entrypoint.sh"]
