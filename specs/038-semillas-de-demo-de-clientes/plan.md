# Plan 038 — Semillas de demo de clientes

> **Jira:** KNOCK-40 · **Estado:** Borrador · **Servicio:** Identidad y confianza. Plan propuesto, sujeto a la aprobación humana de la spec y resolución de sus dudas.

## Enfoque
1. Conciliar el modelo, la frontera de datos, las guardas y la atomicidad de `spec.md` §9 antes de implementar.
2. Reutilizar el formato y patrón de semillas de `db/seeds/catalog/` y la protección y derivación de `src/Knock.Identity.Api/Modules/Sesiones/Services/` (`PhoneProtector`, `UserIdDerivation`); no copiar acceso entre servicios.
3. Preparar el JSON de diez clientes y pruebas de formato y privacidad; extender el acceso a datos de Identidad y conectar el cargador al arranque con las guardas de R-07.
4. Probar idempotencia, protección, conflictos, frontera y humo local; documentar carga, conteo y borrado. El cifrado aleatorio se verifica por descifrado y no por igualdad de bytes.

## Archivos previstos
- `db/seeds/demo/identity/usuarios.json` y `db/seeds/README.md`.
- `src/Knock.Identity.Api/Modules/Sesiones/Data/` y punto de arranque de `Knock.Identity.Api`: ubicación concreta y coordinación del acceso sujetas a §9.
- `tests/DatabaseTest/Demo/` y `tests/UnitTest/Architecture/`: pruebas propuestas de CA-01 a CA-08 y CA-11.
- No se cambian semillas del catálogo ni se agregan dependencias; el script de entrega y respaldos están fuera de alcance.

## Dependencias y riesgos
- Spec 015 y su implementación de `usuarios`, índices únicos, derivación y protección; formato del catálogo de TT-BD-06; `documentdb-local` y fixture existente.
- CA-11 exige ausencia de escrituras parciales incluso ante conflictos; el mecanismo y la identificación del entorno local necesitan decisión (§9).
- Revisión humana de nombres inventados; evidencia sin secretos ni cadenas.

## Verificación
Ejecutar las pruebas de CA-01 a CA-08 y CA-11 con `documentdb-local`, registrar en el PR los dos conteos de CA-09 y ejecutar build y formato de CA-10. Completar los gates aplicables del repositorio y actualizar `Memoria.md` al implementar. Nada de esto se ejecuta durante esta migración documental.
