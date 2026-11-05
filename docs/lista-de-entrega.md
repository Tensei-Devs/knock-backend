# Lista de verificación de la entrega

> Spec 014 (R-06). Se ejecuta **antes** de entregar y se **registra**: copia la tabla del final en el PR o en `Memoria.md` con la fecha, quién la corrió y el resultado real de cada comando. Lo que no se pudo ejecutar se anota como no ejecutado, con el motivo; nunca se marca como hecho.

## Qué se entrega (R-01)
- El proyecto comprimido en `.rar` (tomado del repositorio, ver «Paquete») y, **junto a él**, el script de la base de datos: `db/entrega.js` con sus scripts numerados de `db/` y las semillas de `db/seeds/catalog/`.
- Nada más: sin secretos, sin binarios y sin datos de personas reales (R-03).

## Antes de empaquetar
| # | Verificación | Comando o evidencia | Cubre |
|---|---|---|---|
| 1 | El repositorio está en la rama de entrega, limpio y sin cambios sin subir | `git status` sin cambios · `git log -1` con el commit esperado | R-01 |
| 2 | El CI de esa rama está en verde | Enlace a la ejecución de `gate` | R-06 |
| 3 | Compila sin advertencias y las pruebas pasan | `dotnet build Knock.slnx -c Release -warnaserror` · `dotnet test` de las suites (la integración necesita Docker) | R-06 |
| 4 | Los chequeos del repositorio pasan | `python scripts/sdd_lint.py` · `python scripts/codemap.py --check` · `python scripts/ci/check_exceptions.py` · `python scripts/sync_agents.py --check` | R-06 |
| 5 | El script de la base funciona desde cero | Prueba `Spec014DeliveryScriptTests` (aplica `db/entrega.js` a una instancia vacía y otra vez) | R-02 · CA-01 |
| 6 | Las excepciones registradas no están vencidas | `python scripts/ci/check_exceptions.py` | R-06 |

## Paquete
| # | Verificación | Comando o evidencia | Cubre |
|---|---|---|---|
| 7 | El paquete sale de los archivos versionados, no del disco | `git archive --format=zip -o knock-backend.zip HEAD` (deja fuera `.env`, `appsettings.Development.json`, `bin/`, `obj/` y todo lo ignorado) y se convierte a `.rar` con la herramienta del equipo | R-01 · R-03 |
| 8 | No hay secretos en el paquete | `gitleaks detect --no-git --source <carpeta descomprimida> --config .gitleaks.toml` sin hallazgos | R-03 · CA-02 |
| 9 | No hay binarios ni datos personales reales | Listado del `.rar` sin `.dll`, `.exe`, `.pfx`, `.key` ni archivos de datos; solo semillas ficticias | R-03 |
| 10 | El script de la base viaja con el paquete | `db/entrega.js` presente y probado (fila 5) | R-01 · R-02 |

## Infraestructura y ventana de evaluación (requieren Azure y a una persona con acceso)
| # | Verificación | Comando o evidencia | Cubre |
|---|---|---|---|
| 11 | Ensayo completo: crear, desplegar y destruir desde cero | Comandos de `deploy/README.md` sobre un entorno vacío; el sistema responde `/health` en el gateway | R-04 · CA-03 |
| 12 | Dos copias por servicio durante la ventana | `replicas` en `deploy/apps/envs/evaluacion.tfvars` ≥ 2 para el gateway y cada servicio de negocio; verificación con `az containerapp show` | R-05 · CA-04 |
| 13 | Fuera de la ventana vuelven a una copia | Variable de réplicas y revisión del consumo | Amenaza de sobrecosto |

## Registro de la ejecución
| Fecha | Persona | # | Resultado real | Evidencia |
|---|---|---|---|---|
| | | | | |

## Estado de esta lista
Las filas 1 a 10 se pueden ejecutar sin Azure; la 5 ya tiene prueba automática. Las filas 11 a 13 **no se han ejecutado**: dependen del Terraform (KNOCK-200) y de Azure, y las debe correr una persona autorizada. El paquete `.rar` (fila 7) tampoco se ha generado todavía.
