# El DAST recorre rutas con sesión ficticia

## Qué cambió
- El job «Smoke + DAST» carga índices y catálogo de ejemplo (`db/entrega.js`, servicio `seed` del compose del CI) y arranca Identidad y Mercado con `Seed:Demo`.
- Segundo paso de ZAP baseline con el hook `tests/smoke/zap_rutas.py`: lista de rutas por el proxy de ZAP, con y sin sesión. Las sesiones las firma `tests/smoke/dast_sesion.py` con la llave efímera del job. El primer escaneo no cambia.
- La pila del CI configura las llaves `PanelTotp` (sin ellas `POST /api/v1/panel/sesion/celular` respondía 500) y el `sid` de las sesiones ficticias tiene el formato que espera el backend.
- `security/zap-rules.tsv` ignora además la regla 90004 (falta `Cross-Origin-Resource-Policy`) con EXC-033, hasta el 2026-12-25: TT-API-06 decidió no incluir encabezados `Cross-Origin-*` (sin efecto en JSON). Afecta a los dos escaneos y a todas las variantes de la regla.

## Por qué
- El escaneo anterior veía 3 URL y todas respondían 401: no analizaba ninguna respuesta de la API.
- SonarCloud no corre porque `sonarcloud.yml` no está en `main` (rama predeterminada) y `workflow_run` solo se activa desde ahí; se resuelve con la promoción.

## A evitar
- Ignorar otra regla de ZAP sin su excepción con vencimiento, o renovar EXC-033 sin revisar el informe.
- Quitar las rutas centinela o la comprobación de `dast-rutas.txt`: sin ellas, un token rechazado o un hook que no carga dan un verde falso.
- Cambiar el host `documentdb-local` de la pila del CI: los cargadores de demostración solo aceptan hosts locales.
