# Spec 014 — Entrega del proyecto y reproducibilidad

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir qué se entrega al final del proyecto y cómo se comprueba que todo se puede levantar y tumbar desde cero.

## 2. Alcance
**Dentro**
- Script de la base de datos.
- Proyecto comprimido y su contenido.
- Ventana de evaluación y estado del entorno.
- Lista de verificación antes de entregar.

**Fuera**
- Presentación y material de exposición.

## 3. Requisitos
- **R-01:** El proyecto se entrega comprimido en `.rar` junto con el script de la base de datos.
- **R-02:** El script de la base de datos crea las colecciones, los índices y los datos de ejemplo desde cero y se genera y prueba durante el desarrollo, no al final.
- **R-03:** El archivo entregado no contiene secretos, claves, binarios ni datos personales reales.
- **R-04:** La infraestructura se levanta y se tumba con los comandos documentados.
- **R-05:** Durante la ventana de evaluación cada servicio de negocio y el gateway corren con al menos dos copias.
- **R-06:** Existe una lista de verificación previa a la entrega que se ejecuta y se registra.

## 4. Criterios de aceptación
- **CA-01:** Dada una instancia vacía, cuando se aplica el script de la base, entonces quedan las colecciones, los índices y los datos de ejemplo.
- **CA-02:** Dado el archivo entregado, cuando se busca un secreto, entonces no se encuentra.
- **CA-03:** Dado un entorno vacío, cuando se ejecutan los comandos de crear y desplegar, entonces el sistema responde.
- **CA-04:** Dada la ventana de evaluación, cuando se revisa el entorno, entonces hay dos copias por servicio de negocio.

## 5. Contrato
No aplica.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Secretos en el archivo entregado | Revisión con el escáner de secretos y exclusión de archivos locales antes de comprimir |
| Entorno que no se puede reconstruir en la evaluación | Ensayo completo de crear, desplegar y destruir antes de la fecha |
| Sobrecosto por dejar dos copias encendidas fuera de la ventana | Variable de réplicas y revisión de consumo |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Integración | Sí | Aplicar el script de la base en una instancia vacía |
| Seguridad negativa | Sí | Escaneo de secretos del archivo entregado |
| Humo / extremo a extremo | Sí | Ensayo de crear, desplegar y destruir |
| Unitarias | No | No hay lógica |
| Arquitectura | No | No aplica |

## 8. Decisiones
- **El script de la base se prueba durante el desarrollo:** es parte de la entrega y no debe improvisarse al final.
- **Ensayo previo:** evita sorpresas el día de la evaluación.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-04
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
