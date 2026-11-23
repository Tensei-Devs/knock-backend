// Spec 034 T-04 (R-05, R-06; CA-05, CA-06): k6 durante un despliegue completo de los cuatro componentes con dos réplicas.
// - 20 usuarios virtuales (por omisión) durante DURATION; se lanza el despliegue (ci-deploy o `az containerapp update`) mientras corre.
// - Errores = 5xx y errores de conexión (las 429 NO cuentan). Umbrales: errores < 0.5 % y p95 < 1 s.
// - Cada petición lleva un X-Correlation-Id conocido para contar instancias por componente en Application Insights (docs/OBSERVABILIDAD.md).
// - BASE_URL por variable de entorno; sin URLs, tokens ni datos reales en el código. TOKEN opcional (Authorization: Bearer).
// Ejecutar: BASE_URL=https://<gateway> VUS=20 DURATION=10m k6 run tests/perf/spec034-deploy-sin-corte.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || '').replace(/\/+$/, '');
if (!BASE_URL) {
  throw new Error('Falta BASE_URL (variable de entorno). Ejemplo: BASE_URL=http://localhost:8080');
}
const TOKEN = __ENV.TOKEN || '';
const RUN = __ENV.RUN_ID || `spec034-${Date.now()}`;

// 5xx o error de conexión (status 0); una 429 es limitación, no falla del despliegue.
const errorDeServicio = new Rate('knock_errores_de_servicio');

export const options = {
  scenarios: {
    despliegue: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 20),
      duration: __ENV.DURATION || '10m',
    },
  },
  thresholds: {
    knock_errores_de_servicio: ['rate < 0.005'],
    http_req_duration: ['p(95) < 1000'],
  },
};

function params(correlationId) {
  const headers = { 'X-Correlation-Id': correlationId };
  if (TOKEN) {
    headers.Authorization = `Bearer ${TOKEN}`;
  }
  return { headers, tags: { name: 'despliegue' } };
}

export default function () {
  const correlationId = `${RUN}-${__VU}-${__ITER}`;
  // Rutas anónimas del gateway que atraviesan distintos componentes: ready del gateway y catálogo (Mercado).
  const rutas = ['/health/ready', '/api/v1/oficios'];
  for (const ruta of rutas) {
    const r = http.get(`${BASE_URL}${ruta}`, params(correlationId));
    errorDeServicio.add(r.status === 0 || r.status >= 500);
    check(r, { [`GET ${ruta} sin 5xx ni corte`]: (x) => x.status !== 0 && x.status < 500 });
    // Ninguna respuesta debe revelar la réplica ni el contenedor (R-06).
    check(r, { 'no expone la réplica': (x) => !/replica|revision|container/i.test(Object.keys(x.headers).join(',')) });
  }
  sleep(0.2);
}
