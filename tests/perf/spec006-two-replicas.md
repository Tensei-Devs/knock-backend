# Pruebas de carga — k6

Spec 006 (KNOCK-88): carga contra el **gateway** con 2 réplicas, más el escenario de "caída de una copia".

## Scripts

| Archivo | Qué hace |
|---|---|
| `spec006-two-replicas.js` | Carga constante contra `GET /health/ready` y `GET /api/v1/oficios`. Umbrales: `http_req_failed < 1 %`, `p95 < 500 ms` y un check de que nunca hay respuestas `5xx`. |

## Requisitos

- **k6 instalado** (`k6 version`). Sin k6 no hay números: la salida se reporta tal cual.
- `BASE_URL` por variable de entorno. Sin URLs ni tokens reales en el código.
- Los dos endpoints del script son anónimos en el gateway, así que no hace falta token. Si la ruta objetivo exige sesión, se pasa por la variable `TOKEN` (sin escribir el valor en ningún archivo).

## En local

`deploy/docker-compose.local.yml` hoy solo levanta **infraestructura** (`postgres`, `documentdb` y `redis`): no levanta los servicios .NET ni el gateway, así que no da para "dos instancias detrás del gateway" por sí solo. Para el escenario de 2 réplicas en local hay que levantar las instancias a mano:

1. Infraestructura, si aún no corre (con credenciales por variables, nunca en el repo):

   ```bash
   export KNOCK_MONGO_PASSWORD=<contraseña>
   export KNOCK_REDIS_PASSWORD=<contraseña>
   export POSTGRES_PASSWORD=<contraseña>
   docker compose -f deploy/docker-compose.local.yml up -d documentdb redis
   ```

2. Dos instancias del servicio que se prueba en puertos distintos (ejemplo: Market en `5234` y en `5235`; el resto de servicios según su `appsettings.json`), cada una con su configuración de MongoDB/Redis.

3. El gateway apuntando a ambos destinos (en su `ReverseProxy:Clusters`, dos `Destinations` para ese servicio) y el gateway en su propio puerto.

4. La corrida:

   ```bash
   export BASE_URL=http://localhost:8080
   k6 run tests/perf/spec006-two-replicas.js
   ```

   Pasada corta de humo (sin validar umbrales): `BASE_URL=http://localhost:8080 k6 run --vus 2 --duration 20s tests/perf/spec006-two-replicas.js`

## En Azure

El gateway desplegado corre con **2 réplicas** por variable de Terraform (`deploy/apps/variables.tf`, spec 006, T-01). La URL es la del ingreso público del gateway:

```bash
export BASE_URL=<url-del-gateway-desplegado>
k6 run tests/perf/spec006-two-replicas.js
```

**Escenario de caída de una copia** (manual, ver la cabecera del script): con la prueba corriendo, escala de 2 a 1 (con autorización del líder; nunca contra producción) y verifica que la tasa de error y el `p95` siguen por debajo de los umbrales y que ningún check de `5xx` falla.

## Umbrales

| Métrica | Umbral |
|---|---|
| `http_req_failed` | `< 1 %` |
| `http_req_duration` p95 | `< 500 ms` |
| Respuestas `5xx` | ninguna (check por petición) |
