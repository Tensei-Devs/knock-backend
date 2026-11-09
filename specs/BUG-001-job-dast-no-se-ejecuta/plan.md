# plan.md — BUG-001 El job `dast` del CI nunca se ejecuta por archivos faltantes

> **Estado:** Borrador. No se ejecuta hasta que la spec esté Aprobada.

## 1. Mapeo de archivos
- **Reutilizar (buscar con `codemap.py --query` antes de crear):** imágenes y digests de `deploy/docker-compose.local.yml` (`documentdb-local`, Redis); los `Dockerfile` de `src/Knock.Gateway`, `src/Knock.Identity.Api`, `src/Knock.Market.Api` y `src/Knock.Messaging.Api`; el formato de `security/exceptions.yaml`; el estilo de `deploy/tests/up_test.sh` para la autoprueba en bash.
- **Crear:** `deploy/docker-compose.ci.yml`, `tests/smoke/smoke.sh`, `tests/smoke/smoke_test.sh`, `security/zap-rules.tsv`.
- **Modificar (lo hace una persona; `.github/` está protegido para el agente):** `.github/workflows/ci-pr.yml`: la regla del `gate` para `docker=true` y `dast=false`, y los nombres de los pasos.
- **Modificar (agente):** `docs/QA_DEVSECOPS.md`, `docs/modelo-de-amenazas.md` (CI-6 / H-06), `Memoria.md`.
- **No tocar:** el código de `src/`, salvo que la spec aprobada autorice un perfil de configuración `Ci`; los rulesets (KNOCK-233); los encabezados del gateway (KNOCK-239 y KNOCK-240).

## 2. Riesgos y efectos secundarios
- El job pasa de `skipped` a ejecutarse: los PR que tocan `src/` tardarán más y pueden fallar por hallazgos que hoy no se ven (lo esperado).
- Si la pila no arranca, todos los PR que tocan `src/` fallan en `gate`. Mitigación: espera acotada, registros de los contenedores sin variables de entorno y una corrección rápida.
- Un modo de configuración del CI mal acotado podría quedar disponible en producción. Mitigación: valores sintéticos solo por variables del job y sin banderas que desactiven controles.

## 3. Pruebas por caso
| Caso | Prueba | Suite |
|---|---|---|
| TC-BUG-001-01 | Ejecución real de `ci-pr` en un PR de prueba | (comando) |
| TC-BUG-001-02 | Paso «Levantar» con espera de `/health/ready` | (comando) |
| TC-BUG-001-03 | Paso «Smoke» | (comando) |
| TC-BUG-001-04 | `tests/smoke/smoke_test.sh` con un servidor de prueba (`python3 -m http.server` con un manejador mínimo, o `nc`) | bash |
| TC-BUG-001-05 | Paso «ZAP baseline» con `zap-rules.tsv` | (comando) |
| TC-BUG-001-06 | PR de prueba sin `tests/smoke/smoke.sh` | (comando) |
| TC-BUG-001-07 | Job «Secretos (Gitleaks)» | (comando) |
| TC-BUG-001-08 | Revisión del compose y duración del job | (comando) |

## 4. Secuencia
1. Compose del CI y arranque local con `docker compose -f deploy/docker-compose.ci.yml up` (TC-02).
2. `smoke.sh` y su autoprueba (TC-03 y TC-04).
3. `zap-rules.tsv` y la decisión de §8 sobre KNOCK-239 y KNOCK-240 (TC-05).
4. Una persona aplica el cambio de `ci-pr.yml` (TC-06) en el mismo PR.
5. PR de prueba: ejecución real con la salida de cada caso en el PR (TC-01, 07 y 08).
6. Documentación, `Memoria.md` e informe final.

## 5. Verificación (comandos exactos)
```bash
docker compose -f deploy/docker-compose.ci.yml config --quiet
docker compose -f deploy/docker-compose.ci.yml up -d --build
bash tests/smoke/smoke.sh http://localhost:8080
bash tests/smoke/smoke_test.sh
docker compose -f deploy/docker-compose.ci.yml down -v
gh run view <id> --json jobs -q '.jobs[] | "\(.conclusion) \(.name)"'
```
