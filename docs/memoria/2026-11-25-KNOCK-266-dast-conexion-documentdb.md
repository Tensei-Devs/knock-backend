# El job «Smoke + DAST» nunca había pasado: la pila de CI no conectaba con documentdb-local

## Qué cambió
- `deploy/docker-compose.ci.yml`: `ConnectionStrings__KnockMongo` usa `tls=true&tlsInsecure=true&directConnection=true` en lugar de `tls=true&tlsAllowInvalidCertificates=true`.

## Por qué
- MongoDB.Driver (.NET) ignora `tlsAllowInvalidCertificates`, que es una opción de mongosh. Comprobado con `MongoClientSettings.FromConnectionString`: la cadena anterior daba `AllowInsecureTls=False` y `DirectConnection=False`; la nueva da `True` y `True`.
- Con la cadena anterior, Identity, Market y Messaging rechazaban el certificado autofirmado e intentaban descubrir una réplica: `/health/ready` daba 503 (MongoDB did not respond) y `docker compose --wait` fallaba. Se ve en todas las ejecuciones del job desde el 2026-11-23. `DocumentDbFixture` ya usaba las dos opciones y por eso las pruebas de integración sí conectaban.
- Solo cambia la pila efímera del runner (valores sintéticos, sin Azure); el código de los servicios y la configuración de despliegue no cambian.

## A evitar
- Copiar parámetros de mongosh en cadenas que consume el driver de .NET: el driver ignora en silencio las opciones que no conoce.
- Dar por fallo aleatorio un job que no tiene ninguna ejecución en verde.
