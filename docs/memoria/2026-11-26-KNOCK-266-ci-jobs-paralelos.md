# El job de API se divide en jobs paralelos

## Qué cambió
- `ci-pr.yml`: integración (`test-integration`) y base de datos (`test-database`) corren en jobs propios, en paralelo con compilación, formato, unitarias, arquitectura, seguridad y contrato; `coverage` une las coberturas y aplica los mismos mínimos (60 % líneas, 50 % ramas). El emulador de Firebase pasa a `scripts/ci/firebase_emulator.sh`.
- `gate` exige `success` en los cuatro cuando hay código (un `skipped` ya no basta). `container` y el DAST siguen esperando a todas las pruebas. El artefacto `api-reports` (lo lee `sonarcloud.yml`) lo sube ahora `coverage`.

## Por qué
- En un solo job, integración (24 a 27 min) y base de datos (unos 17) iban una tras otra y el total llegaba a 51 a 55 de los 60 minutos de límite.
- Comprobado antes de subir: los mismos 15 comandos de verificación; el script del `gate` ejecutado con 17 combinaciones; `actionlint`, acciones fijadas y jscpd.

## A evitar
- Añadir un job de pruebas sin incluirlo en `gate` y en `container`.
- Quitar el emulador de integración o de seguridad negativa: las dos lo usan; base de datos y contrato no.
