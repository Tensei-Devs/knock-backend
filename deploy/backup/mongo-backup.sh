#!/usr/bin/env bash
# Respaldo y restauración de una base lógica de MongoDB con mongodump/mongorestore (spec 003, R-08 · TC-003-04).
#
# Uso:
#   mongo-backup.sh dump    <archivo>   copia la base KNOCK_BACKUP_SOURCE_DB a <archivo> (gzip, formato archive)
#   mongo-backup.sh restore <archivo>   restaura <archivo> en KNOCK_BACKUP_TARGET_DB (por defecto, la base de origen)
#
# Variables de entorno (ninguna va en el repositorio ni como argumento de un proceso):
#   KNOCK_BACKUP_MONGO_URI   cadena de conexión; en Azure, desde Key Vault. Obligatoria.
#   KNOCK_BACKUP_SOURCE_DB   base que se respalda (la de Mongo:Databases del servicio). Obligatoria.
#   KNOCK_BACKUP_TARGET_DB   base donde se restaura; debe estar vacía (no se usa --drop). Opcional.
#
# La URI se escribe en un archivo --config temporal con permisos 600 y la salida de las herramientas pasa por un filtro
# que enmascara credenciales. La programación periódica y la subida a Blob Storage dependen de la infraestructura (spec 005).
# Requiere MongoDB Database Tools (mongodump, mongorestore).
set -euo pipefail

usage() {
  echo "Uso: $0 dump|restore <archivo>" >&2
  exit 2
}

[[ $# -eq 2 ]] || usage
mode="$1"
archive="$2"

: "${KNOCK_BACKUP_MONGO_URI:?Define KNOCK_BACKUP_MONGO_URI; la URI no se acepta como argumento.}"
: "${KNOCK_BACKUP_SOURCE_DB:?Define KNOCK_BACKUP_SOURCE_DB.}"
target_db="${KNOCK_BACKUP_TARGET_DB:-$KNOCK_BACKUP_SOURCE_DB}"

name_pattern='^[A-Za-z0-9_-]{1,63}$'
for db in "$KNOCK_BACKUP_SOURCE_DB" "$target_db"; do
  if [[ ! "$db" =~ $name_pattern ]]; then
    echo "Nombre de base no válido (letras, dígitos, '_' o '-')." >&2
    exit 2
  fi
done

umask 077
work="$(mktemp -d "${TMPDIR:-/tmp}/knock-backup.XXXXXX")"
trap 'rm -rf "$work"' EXIT
config="$work/tools.yaml"
printf "uri: '%s'\n" "${KNOCK_BACKUP_MONGO_URI//\'/\'\'}" > "$config"

mask() { sed -E 's#(mongodb(\+srv)?://)[^@/[:space:]]+@#\1***:***@#g'; }

case "$mode" in
  dump)
    mongodump --config="$config" --db="$KNOCK_BACKUP_SOURCE_DB" --archive="$archive" --gzip 2>&1 | mask
    ;;
  restore)
    [[ -f "$archive" ]] || { echo "No existe el archivo de respaldo." >&2; exit 2; }
    # Restauración serial: Azure DocumentDB responde «WriteConflict ... deadlock» si crea colecciones en paralelo
    # (hallazgo de P8 en TT-BD-05).
    status=0
    mongorestore --config="$config" --archive="$archive" --gzip \
      --nsInclude="$KNOCK_BACKUP_SOURCE_DB.*" \
      --nsFrom="$KNOCK_BACKUP_SOURCE_DB.*" --nsTo="$target_db.*" \
      --numParallelCollections=1 --numInsertionWorkersPerCollection=1 \
      2>&1 | mask | tee "$work/restore.log" || status=$?
    if (( status != 0 )) || grep -q "Failed:" "$work/restore.log" \
      || grep -Eq "[1-9][0-9]* document\(s\) failed to restore" "$work/restore.log"; then
      echo "La restauración reportó errores (código $status)." >&2
      exit 1
    fi
    ;;
  *)
    usage
    ;;
esac
