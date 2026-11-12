// Spec 006 (KNOCK-88): k6 contra el gateway con 2 réplicas.
// - BASE_URL llega por variable de entorno; sin URLs ni tokens reales en el código.
// - Sin token el tráfico va anónimo: /health/ready y /api/v1/oficios son anónimos en el gateway.
//   Si hay TOKEN en el entorno se reenvía como "Authorization: Bearer <token>".
//
// Ejecutar en local o en Azure (ver spec006-two-replicas.md de esta carpeta).
//
// Escenario "caída de una copia" (manual, no automatizado):
//   1. Arranca la prueba con las 2 réplicas arriba y verifica el resumen inicial.
//   2. Mientras corre, apaga una réplica a mano:
//      - En local: detén uno de los procesos del servicio (y, si el gateway YARP
//        sigue mandando tráfico a su destino, el apagón es el de ese proceso).
//      - En Azure (Container Apps): escala de 2 a 1 vía tfvars o
//        `az containerapp update --min-replicas 1 --max-replicas 1` (con autorización
//        del líder; no en producción) y espera a que el balanceador la saque de
//        rotación por /health/ready.
//   3. La tasa de error (http_req_failed) y el p95 deben seguir por debajo de los umbrales
//      y ningún check de 5xx puede fallar durante toda la ventana, incluido el apagón.
//   4. Levanta la réplica de nuevo y verifica que /health/ready vuelve a 200.
import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = (__ENV.BASE_URL || '').replace(/\/+$/, '');
if (!BASE_URL) {
  throw new Error('Falta BASE_URL (variable de entorno). Ejemplo: BASE_URL=http://localhost:8080');
}
const TOKEN = __ENV.TOKEN || '';

export const options = {
  scenarios: {
    dos_replicas: {
      executor: 'constant-arrival-rate', // tasa constante de llegadas (el nombre válido del executor de k6)
      rate: Number(__ENV.RATE || 20), // peticiones por segundo
      timeUnit: '1s',
      duration: __ENV.DURATION || '1m',
      preAllocatedVUs: Number(__ENV.PRE_VUS || 20),
      maxVUs: Number(__ENV.MAX_VUS || 100),
    },
  },
  thresholds: {
    http_req_failed: ['rate < 0.01'],
    http_req_duration: ['p(95) < 500'],
  },
};

function headers() {
  return TOKEN ? { Authorization: `Bearer ${TOKEN}` } : {};
}

export default function () {
  const ready = http.get(`${BASE_URL}/health/ready`, { headers: headers() });
  check(ready, {
    'GET /health/ready no responde 5xx': (r) => r.status < 500,
  });

  const oficios = http.get(`${BASE_URL}/api/v1/oficios`, { headers: headers() });
  check(oficios, {
    'GET /api/v1/oficios no responde 5xx': (r) => r.status < 500,
  });
}
