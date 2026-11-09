# Pruebas de carga con k6 (spec 010, T-07)

Escenario de lectura repetida del catálogo público `GET /api/v1/oficios` para ajustar
las vigencias de caché (R-04): compara caché fría (primer acceso) contra caché caliente,
cuenta los `ETag` 200/304 como éxitos (una 304 no es un error) y mide la compresión
(`Content-Encoding: br/gzip`) y las latencias p50/p95/p99 con `http_req_duration`.
Solo hace GET públicos: no necesita credenciales ni escribe datos.

## Requisitos (Mac)

- `k6` instalado: `brew install k6` (o el binario de <https://github.com/grafana/k6/releases>).
- Entorno local aislado: MongoDB (documentdb-local) y Redis levantados con
  `docker compose -f deploy/docker-compose.local.yml up` (requiere `POSTGRES_PASSWORD`,
  `KNOCK_MONGO_USER`, `KNOCK_MONGO_PASSWORD` y `KNOCK_REDIS_PASSWORD`),
  el catálogo sembrado con `mongosh "$KNOCK_DB_URI" db/entrega.js` y el servicio
  Market en `http://localhost:5234` (puerto del Gateway para Mercado en
  `src/Knock.Gateway/appsettings.json`).
- **Nunca** correr contra producción ni servicios compartidos.

Cadena de MongoDB local (solo credenciales de mentira del contenedor, sin secretos reales):

```bash
export KNOCK_DB_URI="mongodb://<usuario>:<contraseña>@localhost:10260/<base>?tls=true&tlsAllowInvalidCertificates=true"
export ConnectionStrings__KnockMongo="$KNOCK_DB_URI"
export ConnectionStrings__KnockRedis="localhost:6379,password=<contraseña>"
export Mongo__Databases__Catalog=market
mongosh --quiet "$KNOCK_DB_URI" db/entrega.js
# Solo entorno local: el límite global por IP (100/min) contaría como error en el script y taparía el efecto de la caché.
export RateLimiting__Global__PermitLimit=1000000
dotnet run --project src/Knock.Market.Api -c Release --urls http://localhost:5234
```

## Ejecutar

```bash
KNOCK_BASE_URL=http://localhost:5234 KNOCK_VUS=5 KNOCK_DURATION=1m KNOCK_RUN_LABEL=ttl-86400 \
  k6 run tests/perf/cache-oficios.k6.js
```

Variables: `KNOCK_BASE_URL` (defecto `http://localhost:5234`), `KNOCK_VUS` (5),
`KNOCK_DURATION` (1m; la ronda fría dura 20 s y la caliente arranca a los 30 s),
`KNOCK_RUN_LABEL` (etiqueta para comparar corridas), `KNOCK_P95_MS` (500).

## Comparar vigencias (R-04)

1. Ajusta `Cache:CatalogLifetimeSeconds` (`.NET` en `src/Knock.Market.Api/appsettings.json`
   o variable `Cache__CatalogLifetimeSeconds`) y reinicia Market.
2. Corre el k6 con un `KNOCK_RUN_LABEL` distinto (p. ej. `ttl-60`, `ttl-86400`).
3. Compara en el resumen de k6 `http_req_duration` (p50/p95/p99) y las métricas
   `cache_cold_duration_ms` / `cache_warm_duration_ms` entre etiquetas.

## Qué garantiza y qué no

Garantiza: el catálogo responde 200/304 sin errores bajo concurrencia constante y
la compresión solo aparece en cuerpos mayores a 1 KB (T-06). No mide escritura,
invalidación al escribir (eso lo cubren T-02 y las pruebas de base de datos) ni
escalado a dos réplicas (eso es TT-QA-02).
