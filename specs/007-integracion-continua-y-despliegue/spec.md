# Spec 007 — Integración continua y despliegue

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir las verificaciones automáticas de cada cambio y cómo el código llega de la computadora del equipo a la nube.

## 2. Alcance
**Dentro**
- Workflow por pull request con filtros por servicio.
- Compilación, formato, pruebas, arquitectura, secretos y dependencias.
- Construcción y publicación de la imagen de cada servicio.
- Despliegue desde la rama principal.

**Fuera**
- Escaneo dinámico (ZAP) y mutación incremental (Stryker): se activan cuando haya un servicio desplegado y una línea de base de pruebas.

## 3. Requisitos
- **R-01:** Cada PR detecta qué servicio cambió y construye y prueba solo ese.
- **R-02:** Cada PR corre restauración, compilación en Release, formato, pruebas unitarias, de integración y de arquitectura.
- **R-03:** Un job busca secretos y otro audita dependencias y licencias.
- **R-04:** Las acciones de terceros se fijan por SHA y los permisos del workflow son los mínimos.
- **R-05:** Al fusionar, el pipeline construye la imagen, la publica en el registro con la etiqueta de versión y despliega.
- **R-06:** Los secretos del pipeline viven en GitHub Secrets.
- **R-07:** Cada PR corre además análisis estático del código (Semgrep con reglas del proyecto), búsqueda de secretos (Gitleaks) y auditoría de dependencias y licencias; Dependabot propone las actualizaciones; y la imagen se escanea con Trivy antes de publicarse. Un hallazgo crítico bloquea.

## 4. Criterios de aceptación
- **CA-01:** Dado un PR que solo toca un servicio, cuando corre el CI, entonces solo se construye ese.
- **CA-02:** Dado un PR con una prueba fallida, cuando corre el CI, entonces el check falla y no se puede fusionar.
- **CA-03:** Dado un cambio aprobado y fusionado, cuando termina el despliegue, entonces la prueba de humo responde.
- **CA-04:** Dado un PR con un secreto de ejemplo o una dependencia con vulnerabilidad crítica, cuando corre el CI, entonces el check falla.

## 5. Contrato
No aplica. Interfaces que fija: los nombres de los checks obligatorios y las etiquetas de imagen con la versión.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Una acción de terceros cambia y roba secretos | Acciones fijadas por SHA y permisos mínimos por job |
| Un secreto se filtra en los registros del pipeline | Los secretos viven en GitHub Secrets y los registros los ocultan |
| Se despliega código sin pasar las pruebas | El despliegue solo corre sobre la rama principal y después del CI en verde |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Scripts de detección de cambios |
| Integración | Sí | Workflow ejecutado sobre un PR de prueba |
| Seguridad negativa | Sí | Secreto de ejemplo en un PR |
| Arquitectura | No | No aplica |
| Contrato | No | No aplica |

## 8. Decisiones
- **Un solo entorno desplegado desde `main`:** simplifica el costo y el proceso.
- **Acciones fijadas por SHA:** reduce el riesgo de cambios inesperados.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
