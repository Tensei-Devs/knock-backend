# Spec 017 — Perfil de la persona

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Servicio:** Identidad y confianza · **Endpoints del Excel:** USR-01 (`GET /api/v1/me`) y USR-02 (`PUT /api/v1/me`) · **Colección:** `usuarios`

## 1. Objetivo
Que la persona con sesión pueda ver y editar su propio perfil (nombre, foto, colonia y código postal). `GET /me` es además la consulta con la que empieza casi toda petición con sesión, por eso es la primera lectura que pasa por la caché de Redis.

## 2. Alcance
**Dentro**
- Leer el perfil propio con caché en Redis y `ETag`.
- Actualizar nombre, foto, colonia y código postal, e invalidar la caché.
- Validación de cada campo.

**Fuera**
- Cambio de número de celular, consentimientos, descarga de datos y eliminación de cuenta (otras specs de `usuarios`).
- Perfil público del técnico (servicio de Mercado).
- Subir la foto: el archivo se sube a Blob Storage por el flujo de archivos y aquí solo se guarda su dirección.

## 3. Requisitos
- **R-01:** `GET /api/v1/me` exige JWT válido y devuelve el perfil de la persona del token: `id` (su `sub`), `nombre`, `fotoUrl`, `colonia`, `codigoPostal`, `esTecnico`, `estado`, `perfilCompleto` y `creadoEn`. Nunca devuelve el celular (queda cifrado) ni datos de otra persona.
- **R-02:** La lectura pasa por un repositorio con caché: clave `perfil:{usuarioId}` en Redis, 60 segundos. Solo va a MongoDB si no está en la caché.
- **R-03:** La respuesta lleva `ETag` débil y `Cache-Control: private, no-cache`; con un `If-None-Match` vigente responde 304 sin cuerpo (spec 010).
- **R-04:** `PUT /api/v1/me` recibe `nombre` (obligatorio), `fotoUrl`, `colonia` y `codigoPostal` (opcionales; omitirlos o mandarlos vacíos los borra). Responde 200 con el perfil actualizado; al quedar el nombre guardado, `perfilCompleto` pasa a verdadero.
- **R-05:** `nombre`: de 2 a 80 caracteres, letras con acentos, espacios, guion y apóstrofo. `codigoPostal`: 5 dígitos. `fotoUrl`: solo una dirección del almacenamiento o de la CDN de Knock (dominios en configuración).
- **R-06:** `colonia` debe existir en el catálogo de colonias, que pertenece al servicio de Mercado; Identidad lo consulta por una interfaz de `Knock.Contracts`, nunca leyendo su base de datos.
- **R-07:** Toda escritura borra la clave `perfil:{usuarioId}` antes de responder y sube `version` y `actualizadoEn`.
- **R-08:** `sub`, `celularCifrado`, `celularHash`, `esTecnico`, `estado`, `perfilCompleto` (salvo por el nombre), `consentimientos` y `creadoEn` no se pueden cambiar por este endpoint: si vienen en el cuerpo se ignoran.
- **R-09:** El perfil se identifica solo por el `sub` del token (se busca en `usuarios.sub`); la ruta no lleva identificador, para que nadie pueda pedir el de otro.
- **R-10:** Los registros no incluyen el celular, el `uid` ni el nombre. Errores en ProblemDetails.
- **R-11:** Si el servicio de Mercado no responde al validar la colonia, `PUT /me` responde 503 y no guarda nada; no se acepta una colonia sin validar.
- **R-12:** Si la cuenta está en eliminación programada, `PUT /me` responde 409 hasta que se cancele la eliminación.

## 4. Criterios de aceptación
- **CA-01:** Dado un JWT válido, cuando se pide `GET /me`, entonces responde 200 con el perfil de esa persona.
- **CA-02:** Dada una segunda lectura sin cambios, cuando se pide, entonces no consulta MongoDB y responde con los mismos datos.
- **CA-03:** Dado un `If-None-Match` vigente, cuando se pide, entonces responde 304 sin cuerpo.
- **CA-04:** Dada una petición sin token o con token cancelado, cuando se pide, entonces responde 401.
- **CA-05:** Dado un `PUT /me` válido, cuando termina, entonces responde 200, el siguiente `GET /me` devuelve los datos nuevos y `version` subió.
- **CA-06:** Dado un `PUT /me` con nombre de un carácter, código postal de 4 dígitos o foto de un dominio ajeno, cuando se pide, entonces responde 422 con `errors` por campo y no cambia nada.
- **CA-07:** Dada una colonia que no existe, cuando se pide el `PUT`, entonces responde 422.
- **CA-08:** Dado un cuerpo con `sub`, `celularHash`, `esTecnico` o `estado`, cuando se hace el `PUT`, entonces esos campos no cambian.
- **CA-09:** Dadas dos personas con sesión, cuando cada una pide `/me`, entonces cada una recibe solo su perfil.
- **CA-10:** Dado un registro de la operación, cuando se escribe, entonces no contiene celular ni nombre.
- **CA-12:** Dado el servicio de Mercado caído, cuando se hace `PUT /me` con colonia, entonces responde 503 y el perfil no cambia.
- **CA-13:** Dada una cuenta en eliminación programada, cuando se hace `PUT /me`, entonces responde 409 y no cambia nada.
- **CA-11:** Dada una cuenta recién creada (`perfilCompleto = false`), cuando guarda su nombre con `PUT /me`, entonces `perfilCompleto` es verdadero y la respuesta de `GET /me` no incluye el celular.

## 5. Contrato
- `GET /api/v1/me` · persona · 200 (perfil), 304, 401.
- `PUT /api/v1/me` · persona · cuerpo `{ nombre, fotoUrl?, colonia?, codigoPostal? }` · 200 (perfil), 400 (cuerpo ilegible), 401, 409, 422 (campos inválidos o colonia inexistente), 503.
- Respuesta de lectura con `ETag` y `Cache-Control: private, no-cache`.
- Interfaz en `Knock.Contracts` para consultar si una colonia existe.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Leer o editar el perfil de otra persona | La identidad sale solo del token y la ruta no lleva identificador |
| Mass assignment: cambiarse `estado` o `esTecnico` | El DTO solo tiene los cuatro campos editables y el resto se ignora |
| Foto con una dirección externa (rastreo, contenido ajeno) | Solo se aceptan direcciones del almacenamiento o la CDN de Knock |
| Datos obsoletos por la caché | Invalidación en cada escritura y vida corta de 60 segundos |
| Fuga de datos personales por los registros | Filtro de datos personales (spec 009) |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validadores de los campos y reglas del perfil |
| Integración | Sí | MongoDB y Redis reales: lectura con caché, escritura e invalidación, 304 |
| Seguridad negativa | Sí | Sin token, token de otra persona, campos protegidos y foto externa |
| Contrato | Sí | Respuestas, `ETag` y errores en el OpenAPI |
| Arquitectura | Sí | El controller no toca MongoDB ni Redis (spec 002) |

## 8. Decisiones
- **Ruta `/me` sin identificador:** evita por diseño que se pida el perfil de otra persona.
- **Caché de 60 segundos con invalidación:** el perfil solo cambia con este `PUT` y con otras acciones de la cuenta, que también invalidan la clave.
- **`PUT` en lugar de `PATCH`:** el Excel define la operación como actualizar los cuatro campos; mandar un campo opcional vacío lo borra.

## 9. Decisiones de la revisión, riesgos y dudas
Decisiones tomadas en la revisión del 2026-11-04. Quien aprueba la spec las acepta; si alguna no convence, se cambia aquí antes de aprobar.

- **Colonia (decidido): se valida siempre contra Mercado; si no responde, 503.** Es mejor no guardar que guardar una colonia inexistente. El catálogo ya existe en `Knock.Market.Api`, así que para la evaluación se despliega junto con Identidad.
- **Cuenta en eliminación programada (decidido): 409.**
- **`If-Match` (decidido): no en esta versión.** Un perfil con cuatro campos editados por una persona no justifica la complejidad; se agrega si aparecen conflictos reales.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-11
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
