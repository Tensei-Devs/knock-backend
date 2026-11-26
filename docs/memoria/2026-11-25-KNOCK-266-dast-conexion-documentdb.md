# El job «Smoke + DAST» nunca había pasado: la pila de CI no conectaba con documentdb-local

## Qué cambió
- `deploy/docker-compose.ci.yml`: `ConnectionStrings__KnockMongo` usa `tls=true&tlsInsecure=true&directConnection=true` en lugar de `tls=true&tlsAllowInvalidCertificates=true`.

## Por qué
- MongoDB.Driver (.NET) ignora `tlsAllowInvalidCertificates`, que es una opción de mongosh. Comprobado con `MongoClientSettings.FromConnectionString`: la cadena anterior daba `AllowInsecureTls=False` y `DirectConnection=False`; la nueva da `True` y `True`.
- Con la cadena anterior, Identity, Market y Messaging rechazaban el certificado autofirmado e intentaban descubrir una réplica: `/health/ready` daba 503 (MongoDB did not respond) y `docker compose --wait` fallaba. Se ve en todas las ejecuciones del job desde el 2026-11-23. `DocumentDbFixture` ya usaba las dos opciones y por eso las pruebas de integración sí conectaban.
- Solo cambia la pila efímera del runner (valores sintéticos, sin Azure); el código de los servicios y la configuración de despliegue no cambian.

## ZAP baseline (primera ejecución real)
- Con la pila arriba, ZAP dio 0 `FAIL` y 67 reglas aprobadas; el job fallaba por un único `WARN` informativo (10049, `no-store`), porque `zap-baseline` sale con código 2 ante cualquier `WARN`. José autorizó ignorar solo esa regla con EXC-032 (30 días). `no-store` no cambia y el gate sigue activo.
- Limitación: el escaneo es sin sesión (2 endpoints, todo 4xx). No cubre rutas autenticadas y no valida la seguridad de la API.

## Segunda causa (2026-11-26, PR #401)
- En la integración final, el job «API — build, formato y pruebas» pasó, pero «Smoke + DAST» volvió a fallar: `docker compose --wait` dejó `market`, `identity` y `messaging` unhealthy con `MongoDB did not respond (TimeoutException)` en `/health/ready`.
- Los servicios arrancaban en cuanto `documentdb` estaba **started**, no cuando ya aceptaba conexiones TLS; con cuatro imágenes .NET construyéndose a la vez, la carrera era más frecuente que en el PR #398.
- **Cambio:** `documentdb` con healthcheck TCP al puerto 10260 y `depends_on: service_healthy`; sondas de los servicios con `start_period` 60 s y 24 reintentos (misma línea de espera que `DocumentDbFixture`, 90 s).

## A evitar
- Copiar parámetros de mongosh en cadenas que consume el driver de .NET: el driver ignora en silencio las opciones que no conoce.
- Dar por fallo aleatorio un job que no tiene ninguna ejecución en verde.
- Presentar el DAST en verde como una auditoría de la API autenticada.
- Arrancar los servicios con `service_started` en documentdb-local sin sonda: el puerto abre antes de que el servidor responda.
