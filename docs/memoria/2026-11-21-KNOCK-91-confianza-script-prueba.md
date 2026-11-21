# Prueba de base de datos del script de técnicos de confianza (spec 042, T-01)

## Qué cambió
- `Spec042TrustScriptTests` en `DatabaseTest` comprueba que `db/042-tecnicos-de-confianza.js` crea `tecnicos_confianza` con el índice único (clienteId, tecnicoId), que reaplicarlo no borra datos, que el par repetido falla con clave duplicada y que un nombre de base inválido termina con código distinto de 0.
- La fila T-01 de `task.md` pasa a «hecha».

## Por qué
Era lo único que faltaba de T-01 (KNOCK-91, §6 de la spec); corre solo en CI con Docker.

## A evitar
- No ejecutar el script contra una base real para probarlo: usar la base temporal de la prueba.