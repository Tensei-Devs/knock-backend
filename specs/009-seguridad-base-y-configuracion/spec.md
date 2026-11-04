# Spec 009 — Seguridad base y configuración

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir los controles de seguridad comunes a todos los servicios y cómo se maneja la configuración y los secretos, sin implementar flujos de negocio.

## 2. Alcance
**Dentro**
- Configuración por entorno y gestión de secretos.
- Cabeceras de seguridad, CORS y límite de peticiones en el gateway.
- Esquema general de autenticación con JWT y autorización por recurso.
- Registros sin datos personales.

**Fuera**
- Flujos de inicio de sesión y de registro.
- Qué campos concretos se cifran en cada colección (lo fija la spec de cada endpoint; aquí solo la regla general).
- INE, selfie y su consentimiento: regla de la constitución del equipo (§6) que necesita su propia spec antes de implementarse.

## 3. Requisitos
- **R-01:** Ningún secreto vive en el repositorio ni en los registros; los secretos están en Key Vault o en variables.
- **R-02:** La API envía cabeceras de seguridad (HSTS, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`).
- **R-03:** CORS permite solo los orígenes configurados.
- **R-04:** El gateway limita peticiones por IP antes de autenticar y por usuario después, con contadores en Redis.
- **R-05:** El JWT de acceso dura 15 minutos y se valida con un solo algoritmo (HMAC-SHA256) y una clave de al menos 32 bytes; todo recurso valida la autorización en el servidor.
- **R-06:** Los registros no incluyen celulares, correos, direcciones ni documentos.
- **R-07:** Los datos personales sensibles (celular, dirección, documentos de identidad) se guardan cifrados a nivel de campo con AES-256-GCM; para buscarlos se usa un HMAC-SHA256 con una llave distinta de la de cifrado, y las dos llaves viven en Key Vault.
- **R-08:** Cerrar sesión cancela el token: el identificador del token cancelado se guarda en Redis hasta que vence y el gateway rechaza con 401 todo token cancelado. El token de renovación se guarda solo como huella.

## 4. Criterios de aceptación
- **CA-01:** Dada una petición sin token a una ruta protegida, cuando se hace, entonces responde 401.
- **CA-02:** Dada una respuesta cualquiera, cuando se inspeccionan las cabeceras, entonces están las de seguridad.
- **CA-03:** Dado un registro con un celular, cuando se escribe, entonces el dato no aparece en la salida.
- **CA-04:** Dado un token firmado con otro algoritmo, cuando se presenta, entonces se rechaza.
- **CA-05:** Dado un campo de celular guardado, cuando se lee directo de la base, entonces no aparece en claro y la búsqueda por HMAC lo encuentra.
- **CA-06:** Dado un token cancelado que aún no vence, cuando se presenta al gateway, entonces responde 401.

## 5. Contrato
Fija las cabeceras de seguridad de toda respuesta, el esquema de autenticación con JWT en `Authorization: Bearer` y la forma de los errores 401 y 403 (ProblemDetails).

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Suplantación: token falsificado o con otro algoritmo | Un solo algoritmo (HMAC-SHA256), clave de al menos 32 bytes en Key Vault y validación de firmante, destinatario y vencimiento |
| Fuerza bruta o abuso de rutas de autenticación | Límite de peticiones por IP y por usuario, más estricto en autenticación |
| Fuga de datos personales por los registros | Filtro que oculta celulares, correos, direcciones y documentos |
| Acceso a recursos ajenos | Autorización por recurso decidida en el servidor y denegar por defecto |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validación del token y política por defecto |
| Integración | Sí | Pipeline completo con autenticación |
| Seguridad negativa | Sí | Entrar sin sesión, token manipulado, recurso ajeno y datos maliciosos |
| Contrato | Sí | Cabeceras y errores 401 y 403 en el OpenAPI |
| Arquitectura | No | Se cubre en la spec 001 |

## 8. Decisiones
- **Denegar por defecto:** toda ruta exige autenticación salvo las marcadas como públicas.
- **Una sola entrada:** la seguridad, el límite de peticiones y el registro se aplican en un único punto.
- **Secretos fuera del código:** protege al repositorio aunque se haga público.

## 9. Riesgos y dudas abiertas
- ¿Se cancela por el identificador del token (`jti`, como dice la constitución del equipo) o por el de la sesión (`sid`, como usan las specs 016 y 018)? Decide José Julián con Guillermo; las specs 016 y 018 se alinean con lo que se decida.
- Si Redis se reinicia, un token cancelado vale hasta 15 minutos más; el riesgo está aceptado (spec 003), la renovación cancelada sigue bloqueada porque su huella se guarda en MongoDB.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
