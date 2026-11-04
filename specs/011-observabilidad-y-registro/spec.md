# Spec 011 — Observabilidad y registro

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir cómo se registran los eventos y errores de todos los servicios en un lugar central y cómo se sigue una petición entre servicios.

## 2. Alcance
**Dentro**
- Registro estructurado con Serilog.
- Envío a Application Insights con Log Analytics.
- Identificador de correlación `X-Correlation-Id`.
- Niveles de gravedad, retención y alertas.
- Prohibición de datos personales en los registros.

**Fuera**
- Tableros y métricas de negocio.
- Respuesta a incidentes.

## 3. Requisitos
- **R-01:** Los tres servicios y el gateway envían sus registros a Application Insights.
- **R-02:** Toda petición lleva `X-Correlation-Id`; si no llega, se genera, y se reenvía en cada llamada entre servicios.
- **R-03:** Los registros tienen nivel de gravedad y formato estructurado.
- **R-04:** Los registros no incluyen celulares, correos, direcciones ni documentos.
- **R-05:** Hay alertas para picos de errores y para el consumo del presupuesto.
- **R-06:** La retención es corta para no consumir la cuota gratuita.
- **R-07:** Los registros se consultan desde el portal o con el CLI de Azure.

## 4. Criterios de aceptación
- **CA-01:** Dada una petición que cruza dos servicios, cuando se busca su identificador, entonces aparecen los registros de ambos.
- **CA-02:** Dado un registro con un celular, cuando se escribe, entonces el dato no aparece.
- **CA-03:** Dado un pico de errores, cuando supera el umbral, entonces se dispara la alerta.

## 5. Contrato
Fija el encabezado `X-Correlation-Id` y el formato de los registros (campos `timestamp`, `level`, `service`, `correlationId`, `message`).

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Datos personales en los registros | Filtro de enmascarado y una prueba que pasa un celular y una CURP por Serilog y comprueba que no salen |
| Registros manipulados o borrados | Los registros viven en un servicio central con acceso por permisos; los servicios solo escriben |
| Sobrecosto por exceso de registros | Retención corta, niveles por entorno y alertas de consumo |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Formato y enmascarado de datos personales |
| Integración | Sí | Un servicio escribiendo en un receptor de pruebas |
| Seguridad negativa | Sí | Datos personales de ejemplo no salen |
| Humo / extremo a extremo | Sí | Una petición real aparece en Application Insights |
| Arquitectura | No | No aplica |
| Contrato | No | No hay contrato de negocio |

## 8. Decisiones
- **Registro central:** buscar un error en cada pieza sería lento; en un solo lugar se ve dónde falló.
- **Identificador de correlación:** permite reconstruir el recorrido completo de una petición.

## 9. Riesgos y dudas abiertas
- La cuota gratuita de registros puede agotarse si se deja un nivel muy detallado.
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-04
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
