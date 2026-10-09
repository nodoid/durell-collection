#!/usr/bin/env bash
# One-off: creates the Google Play upload key for The Durell Collection in signing/ (git-ignored).
# BACK UP signing/ somewhere safe - you need this key for every future Play update.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck disable=SC1090
[ -f "$ROOT/signing/local.properties" ] && source "$ROOT/signing/local.properties"
mkdir -p "$ROOT/signing"
KS="$ROOT/signing/durell-upload.keystore"
[ -e "$KS" ] && { echo "$KS already exists"; exit 1; }
PASS=$(openssl rand -base64 24 | tr -d '/+=' | cut -c1-24)
keytool -genkeypair -v -keystore "$KS" -alias durell -keyalg RSA -keysize 2048 -validity 10000 \
  -storepass "$PASS" -keypass "$PASS" -dname "${KEY_OWNER:-CN=The Durell Collection}" >/dev/null
cat > "$ROOT/signing/android.properties" <<PROPS
KEYSTORE_FILE=durell-upload.keystore
KEY_ALIAS=durell
export DURELL_STORE_PASS='$PASS'
PROPS
chmod 600 "$ROOT/signing/"*
echo "Created $KS (password in signing/android.properties)"
