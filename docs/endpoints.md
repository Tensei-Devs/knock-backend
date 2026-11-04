# Endpoints de la API

> AGENTS.md §5. Una fila por operación HTTP de negocio, con su spec. El contrato completo se genera del código (`GET /openapi/v1.json`, solo en Development); cada servicio se exporta a `docs/` (sección «Contrato OpenAPI por servicio» al final).
> Límites de peticiones: `docs/rate-limit-matrix.md` (la política global aplica a todas las filas salvo que se diga otra cosa).
> Rastreo (todas las filas): cada respuesta lleva el encabezado `X-Correlation-Id` y cada error ProblemDetails, los campos `correlationId` (mismo valor) y `traceId` (W3C, `<trace-id>` de 32 hex). Formatos y respuestas en la §4 de la spec TT-OBS-01 y en `docs/MIDDLEWARES.md`.

| Método y ruta | Módulo | Auth | Respuesta | Caché | Spec | Notas |
|---|---|---|---|---|---|---|
| `GET /api/v1/oficios` | Catalog | pública (`[AllowAnonymous]`) | 200 `application/json`: arreglo de `{ clave, nombre, modalidad }` | `86400` s (declarada; sin implementar) | TT-PRU-01 | Solo oficios activos, ordenados por nombre sin importar mayúsculas y luego por clave. Arreglo sin paginar: excepción a la constitución §11 (catálogo acotado; si pasa de 50 elementos, `CursorPage<T>` en `v2`). `modalidad` es texto con valores conocidos `domicilio` y `taller`, no enum cerrado. |
| `GET /api/v1/colonias/en-punto` | Catalog | JWT Bearer | 200 `application/json`: `{ enCobertura, colonia: { id, nombre, cp, municipio } }` (`colonia` es `null` si el punto está fuera de cobertura); 401, 422 (`errors.lat` o `errors.lon`), 429 y 500 en ProblemDetails | sin caché (`Cache-Control: no-store`) | TT-API-07 | Query `lat` y `lon`: texto, un valor cada uno, patrón `^-?\d{1,3}(\.\d{1,10})?$`, máx. 16 caracteres y rangos [-90, 90] y [-180, 180]. No guarda ni registra coordenadas. Límite global por IP, sin exención. KNOCK-141, casos TC-TT-API-07-03 a 16. |
| `POST /api/v1/auth/firebase` | Identity (Sesiones) | pública (`[AllowAnonymous]`) | 201 si crea la cuenta y 200 si ya existía, `application/json`: `{ accessToken, refreshToken, esNuevo, perfilCompleto }`; 401 (token de Firebase inválido), 403 `account_suspended`, 409 `account_conflict`, 422, 429 y 500 en ProblemDetails | sin caché (`Cache-Control: no-store`) | 015 | Cuerpo `{ firebaseIdToken, deviceId, aceptaAviso, dispositivo }`. Registro e inicio de sesión en un endpoint. El JWT lleva solo `sub` y `sid` (id de la sesión guardada en `sesiones`); el token de renovación es opaco y solo se guarda su huella. Máximo 10 sesiones abiertas: la 11.ª revoca la más antigua. Política estricta `/auth` (10 por minuto por IP). KNOCK-210. |

## Contrato OpenAPI por servicio

Cada servicio con API publica su contrato en `docs/` (spec 002, R-07). Los archivos se generan, nunca se editan a mano:

| Servicio | Archivo | Regenerar |
|---|---|---|
| Market | `docs/openapi.v1.json` | `python scripts/export_openapi.py` |
| Identity | `docs/openapi.identity.v1.json` | `python scripts/export_openapi.py --service identity` |

- El script arranca el servicio en Development en un puerto libre, lee `/openapi/v1.json`, quita `servers` y guarda JSON estable con saltos LF. Con `--no-build` usa la compilación Release existente (así lo llama el CI); con `--output ruta.json` guarda en otra ruta.
- No necesita bases de datos ni credenciales: usa cadenas inalcanzables y, para Identity, claves y contraseñas sintéticas que se generan al azar en cada ejecución y solo viven en el proceso (nada se escribe en disco).
- El CI regenera ambos y falla si `git diff --exit-code` ve cambios en alguno. Si cambias un endpoint, un DTO o un comentario XML que llegue al contrato, regenera el archivo del servicio y súbelo en el mismo commit.
- `oasdiff` compara cada contrato contra el de la rama base y falla ante cambios incompatibles. El nombre de Market se conserva para no romper la publicación al frontend.
- Pruebas del script: `python scripts/test_export_openapi.py`.
