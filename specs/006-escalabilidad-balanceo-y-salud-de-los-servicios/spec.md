# Spec 006 — Escalabilidad, balanceo y salud de los servicios

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir cómo corre cada servicio en varias copias detrás de un balanceador y qué debe cumplir el código para que eso funcione.

## 2. Alcance
**Dentro**
- Dos copias o más por servicio y el balanceador de Container Apps.
- Rutas de salud, encabezados reenviados y estado compartido.
- Caché y límite de peticiones compartidos en Redis.
- Relación entre escalado fijo y costo.

**Fuera**
- Autoescalado, que no es un requerimiento.
- Pruebas de carga detalladas.

## 3. Requisitos
- **R-01:** Cada servicio de negocio y el gateway corren con al menos dos copias durante la ventana de evaluación, fijadas por variable de Terraform y sin reglas de autoescalado.
- **R-02:** El balanceador usa `/health/ready` para decidir si manda tráfico a una copia.
- **R-03:** El servidor lee los encabezados reenviados para conocer la IP real de la persona y solo confía en el balanceador.
- **R-04:** Los tokens se firman con una clave compartida de Key Vault y cualquier copia los acepta.
- **R-05:** La lista de tokens cancelados, la caché y los contadores del límite de peticiones viven en Redis, no en memoria.
- **R-06:** Fuera de la ventana de evaluación, el número de copias baja para cuidar el costo.

## 4. Criterios de aceptación
- **CA-01:** Dadas dos copias de un servicio, cuando una se apaga, entonces la otra atiende y no se pierde la sesión.
- **CA-02:** Dado un token emitido por una copia, cuando lo presenta a la otra, entonces lo acepta.
- **CA-03:** Dada una copia con MongoDB inaccesible, cuando se pide `/health/ready`, entonces no recibe tráfico.

## 5. Contrato
Fija las rutas de salud de cada servicio: `/health` (el proceso vive) y `/health/ready` (puede hablar con MongoDB y Redis), usadas por el balanceador.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Una copia con una IP falsa en los encabezados reenviados engaña al límite de peticiones | El servidor solo confía en los encabezados del balanceador (redes conocidas) |
| Una copia enferma recibe tráfico y devuelve errores | El balanceador usa `/health/ready` y la saca de rotación |
| Estado en memoria distinto entre copias da respuestas incoherentes | Tokens cancelados, caché y contadores viven en Redis |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Lectura de encabezados reenviados |
| Integración | Sí | Dos copias y Redis reales |
| Rendimiento | Sí | Reparto de carga y caída de una copia |
| Seguridad negativa | Sí | Encabezados reenviados falsos |
| Arquitectura | No | Se cubre en la spec 001 |
| Contrato | No | No hay contrato de negocio |

## 8. Decisiones
- **Escalado y no autoescalado:** nosotros fijamos cuántas copias hay; no es un requerimiento tener reglas que las suban solas.
- **Estado en Redis:** una caché en memoria daría respuestas distintas según la copia.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-04
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
