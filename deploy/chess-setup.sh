#!/usr/bin/env bash
# Root part of the FPS Chess server deployment on gulfinson. Idempotent; run once with
#   sudo bash ~/websites/chess.gulfgulfinson.de/setup/chess-setup.sh
# Needs the proxied Cloudflare record "chess" to exist before the certificate step.
set -euo pipefail

host=chess.gulfgulfinson.de
here=$(cd "$(dirname "$0")" && pwd)
[[ $EUID -eq 0 ]] || { echo "Bitte mit sudo ausführen." >&2; exit 1; }

# A reload can exit 0 and still keep the old configuration (README §4): compare the worker PIDs.
reload_nginx() {
  nginx -t
  local main before after
  main=$(systemctl show nginx -p MainPID --value)
  before=$(pgrep -P "$main" | sort | tr '\n' ' ')
  systemctl reload nginx
  sleep 2
  after=$(pgrep -P "$main" | sort | tr '\n' ' ')
  if [[ "$before" == "$after" ]]; then
    echo "[setup] nginx reload did not take, restarting nginx"
    systemctl restart nginx
  fi
  grep -v "protocol options redefined" /var/log/nginx/error.log | tail -3 || true
}

echo "[setup] systemd unit"
install -m 644 "$here/webapp-chess.service" /etc/systemd/system/webapp-chess.service
systemctl daemon-reload
systemctl enable --now webapp-chess.service
sleep 4
systemctl is-active webapp-chess.service

echo "[setup] logrotate"
install -m 644 "$here/logrotate-webapp-chess" /etc/logrotate.d/webapp-chess
logrotate -d /etc/logrotate.d/webapp-chess >/dev/null

echo "[setup] restart permission for manual deploys"
bash /home/pschneid/Gulfslab/scripts/setup-deploy-permissions.sh

if [[ ! -f /etc/letsencrypt/live/$host/fullchain.pem ]]; then
  echo "[setup] nginx stage 1 and certificate"
  install -m 644 "$here/$host.http-only.conf" /etc/nginx/sites-available/$host.conf
  ln -sfn /etc/nginx/sites-available/$host.conf /etc/nginx/sites-enabled/$host.conf
  reload_nginx
  certbot certonly --webroot -w /var/www/html -d "$host" --non-interactive --keep-until-expiring
fi

echo "[setup] nginx final vhost"
install -m 644 "$here/$host.conf" /etc/nginx/sites-available/$host.conf
ln -sfn /etc/nginx/sites-available/$host.conf /etc/nginx/sites-enabled/$host.conf
reload_nginx

echo "[setup] check"
curl -sS -o /dev/null -w "local https status: %{http_code}\n" --resolve "$host:443:127.0.0.1" "https://$host/"
echo "[setup] done"
