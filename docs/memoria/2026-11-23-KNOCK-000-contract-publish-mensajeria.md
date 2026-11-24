# 2026-11-23 · KNOCK-000 · contract-publish incluye el contrato de Mensajería

## Qué cambió
`.github/workflows/contract-publish.yml` ahora dispara también con cambios en `docs/openapi.messaging.v1.json` y pasa ese archivo a `scripts/ci/merge_openapi.py` con la opción nueva `--messaging`, de modo que `contracts/openapi.v1.json` del frontend combina Market, Identidad y Mensajería. Sin pasos ni secretos nuevos. `merge_openapi.py` omite del lado de Mensajería solo las rutas internas que ya publican los otros servicios (`/health` y `/interno/*`); cualquier otra ruta o esquema repetido sigue fallando. `scripts/ci/test_merge_openapi.py` suma cuatro pruebas (unión con Mensajería, ruta pública repetida, versión distinta y los contratos reales del repositorio).

## Por qué
El flujo solo publicaba Market e Identidad, así que las rutas de conversaciones, avisos y dispositivos nunca llegaban al cliente Dart del frontend. Mensajería comparte `/health` con Identidad y dos rutas `/interno/` con Market; sin la omisión el script habría fallado por ruta repetida.

## A evitar
- Publicar `docs/openapi.publico.v1.json` en este flujo: no se hace todavía. La spec 034 (R-17) lo prevé como contrato del frontend, pero cambia la forma del contrato vendorizado (solo rutas del gateway, sin `servers`) y el cliente Dart y el `oasdiff` del frontend dependen del combinado actual. Cambiarlo es una decisión del equipo de frontend; queda pendiente.
- Dar por hecha la primera ejecución real: sigue pendiente (T-10 de la spec 034).
