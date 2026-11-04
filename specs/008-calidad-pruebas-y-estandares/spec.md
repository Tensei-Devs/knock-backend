# Spec 008 — Calidad, pruebas y estándares

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir las reglas de código y los tipos de pruebas que se esperan en todo el repositorio.

## 2. Alcance
**Dentro**
- `.editorconfig` y analizadores de .NET.
- Convenciones de nombres y complejidad máxima.
- Tipos de pruebas y cobertura mínima.
- Definición de terminado.

**Fuera**
- Pruebas de rendimiento y de mutación (se activan después).

## 3. Requisitos
- **R-01:** Los warnings se tratan como errores.
- **R-02:** La complejidad ciclomática por método es de 10 como máximo.
- **R-03:** Código y comentarios técnicos en inglés; specs y documentación en español.
- **R-04:** Cada criterio de aceptación de una spec tiene al menos una prueba que falla si la lógica está mal.
- **R-05:** La cobertura mínima es del 60 % de líneas y del 50 % de ramas.
- **R-06:** Las pruebas de integración y de base de datos usan servicios reales en contenedor con Testcontainers (`documentdb-local` para MongoDB, y Redis); los dobles solo en pruebas unitarias.
- **R-07:** No se agregan dependencias sin evaluar necesidad, licencia, mantenimiento y vulnerabilidades.
- **R-08:** El nombre de cada prueba lleva el identificador del criterio que cubre (por ejemplo, `..._CA_01`), para poder trazar criterio y prueba con un script.

## 4. Criterios de aceptación
- **CA-01:** Dado un método con complejidad mayor a 10, cuando se compila, entonces el analizador lo marca.
- **CA-02:** Dado un PR que baja la cobertura del mínimo, cuando corre el CI, entonces falla.
- **CA-03:** Dada una dependencia nueva, cuando se revisa el PR, entonces `Memoria.md` registra su evaluación.
- **CA-04:** Dado un criterio de aceptación de una spec Aprobada sin ninguna prueba que lo nombre, cuando corre el script de trazabilidad, entonces lo reporta.

## 5. Contrato
No aplica.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Pruebas debilitadas o desactivadas para que pase el CI | Los umbrales no se bajan; la constitución lo prohíbe y la revisión lo comprueba |
| Dependencia con vulnerabilidad o licencia incompatible | Evaluación previa registrada en `Memoria.md` y auditoría automática en el CI |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Analizadores y reglas |
| Integración | Sí | El CI sobre un PR de prueba |
| Arquitectura | Sí | Complejidad y fronteras |
| Seguridad negativa | No | Se cubre en la spec 009 |
| Contrato | No | No aplica |

## 8. Decisiones
- **Warnings como errores:** evita acumular deuda sin verla.
- **Real primero:** las pruebas de integración con servicios reales detectan problemas que los dobles ocultan.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-04
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
