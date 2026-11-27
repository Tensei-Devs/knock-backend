# Evaluación contrastada y registro de versiones desplegadas (KNOCK-266)

## Qué cambió

- `docs/EVALUACION.md` se contrastó con el código de `sprint-05` (`75d5ad0e`), la corrida 45883514523 y los ajustes de GitHub en solo lectura. Se corrigieron las cifras de pruebas y de operaciones, la moderación del chat en dev, y se agregaron CodeQL, SonarCloud, los ajustes de GitHub y la tabla de equivalencias del Excel.
- `docs/VERSIONES_DESPLEGADAS.md` tiene las primeras filas: seis despliegues a dev, sin versión `X.Y.Z` porque nunca se asignó una.
- `COL-01` (`GET /colonias`) queda anotado como no implementado y pendiente de decisión.

## Por qué

- La versión anterior citaba una corrida cancelada de otra rama y no decía que CodeQL no se usa ni que SonarCloud no corre.
- Un número de versión calculado después del despliegue no coincidiría con ninguna etiqueta de imagen ni de Git: se registra el SHA, que sí existe.

## A evitar

- Citar como corrida de la rama una corrida de PR; comprobar rama, evento y conclusión con `gh run view`.
- Perder la tabla de equivalencias del Excel al reescribir `docs/EVALUACION.md`: sin ella los endpoints con otra ruta parecen no creados.
- Copiar a un documento público identificadores de la suscripción o de principales que aparezcan en notas de despliegue.
