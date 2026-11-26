#!/usr/bin/env bash
# TT-CI-03 (KNOCK-139): emulador de Firebase Auth para las pruebas de inicio de sesión (integración y seguridad negativa). Sin Java ni
# credenciales: solo el proyecto ficticio demo-knock. firebase-tools queda fijado a una versión exacta. Deja la dirección en GITHUB_ENV.
set -eo pipefail
cfg="$RUNNER_TEMP/firebase-emulator"
mkdir -p "$cfg"
cat > "$cfg/firebase.json" <<'JSON'
{"emulators":{"auth":{"host":"127.0.0.1","port":9099},"ui":{"enabled":false}}}
JSON
(cd "$cfg" && nohup npx --yes firebase-tools@15.34.0 emulators:start --only auth --project demo-knock > "$cfg/emulator.log" 2>&1 &)
for _ in $(seq 1 90); do
  curl -fsS http://127.0.0.1:9099/ > /dev/null 2>&1 && break
  sleep 1
done
if ! curl -fsS http://127.0.0.1:9099/ > /dev/null 2>&1; then
  echo "::error::El emulador de Firebase Auth no respondió en 90 s"
  cat "$cfg/emulator.log"
  exit 1
fi
echo "FIREBASE_AUTH_EMULATOR_HOST=127.0.0.1:9099" >> "$GITHUB_ENV"
