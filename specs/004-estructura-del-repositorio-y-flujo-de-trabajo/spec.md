# Spec 004 — Estructura del repositorio y flujo de trabajo

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Fijar cómo está organizado el repositorio, cómo se nombran las ramas y los commits y cómo se revisa un cambio, para que el equipo y los agentes de IA trabajen igual.

## 2. Alcance
**Dentro**
- Carpetas de la raíz y su propósito.
- Ramas `main`, `develop` y de trabajo.
- Convención de commits y de ramas.
- Plantilla de pull request y CODEOWNERS.
- Archivos de gobierno: `README.md`, `AGENTS.md`, `constitution.md` y `Memoria.md`.

**Fuera**
- Reglas de protección configuradas en GitHub.
- Automatización de CI (spec 007).

## 3. Requisitos
- **R-01:** `main` contiene solo código listo para publicar y `develop` integra los cambios.
- **R-02:** Cada cambio vive en una rama `<tipo>/<descripcion-en-ingles>` creada desde `develop`.
- **R-03:** Los commits siguen Conventional Commits en inglés.
- **R-04:** Todo cambio entra por pull request con descripción, pruebas ejecutadas y la revisión del líder técnico, que es otra persona respecto de quien escribió el cambio y es la obligatoria; el comentario de un integrante es opcional y no bloquea la fusión. Los cambios de seguridad, infraestructura, CI, scripts y Terraform conservan además un comentario de otra persona con contexto, salvo que sea urgente o no haya nadie disponible. Los pull request del líder técnico los puede aprobar él mismo; en esas zonas sensibles pide a otra persona la revisión, y si es urgente el PR anota el motivo.
- **R-05:** Una spec aprobada precede al código de cada cambio.
- **R-06:** `Memoria.md` se actualiza al cerrar cada cambio.
- **R-07:** La plantilla de pull request tiene las secciones Descripción, Pruebas, Seguridad, Riesgos y «Uso de IA» (qué generó la IA y qué revisó una persona).

## 4. Criterios de aceptación
- **CA-01:** Dado un commit con otro formato, cuando se valida, entonces se rechaza.
- **CA-02:** Dado un PR de una persona distinta del líder técnico sin ninguna revisión, cuando se intenta fusionar, entonces no se permite.
- **CA-06:** Dado un PR del líder técnico, urgente o sin nadie disponible para revisarlo, cuando lo aprueba y fusiona él mismo, entonces el PR anota el motivo.
- **CA-03:** Dado el repositorio, cuando se abre `README.md`, entonces explica cómo compilar y probar.
- **CA-04:** Dado un PR sin la sección «Uso de IA», cuando corre la comprobación de la plantilla, entonces falla.

## 5. Contrato
No aplica. Interfaces que fija: el formato `tipo(alcance): descripcion` de los commits y la plantilla de PR.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Cambios sin revisión llegan a `main` | Protección de ramas: PR obligatorio, revisión de otra persona y checks en verde |
| Un secreto entra al historial por un commit | Hook y job de CI que buscan secretos antes de fusionar |
| Un agente de IA fusiona o aprueba por su cuenta | La constitución prohíbe a los agentes aprobar y fusionar; CODEOWNERS exige revisión humana |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validador de formato de commit y de rama |
| Integración | Sí | Hooks de Git en un repositorio de prueba |
| Seguridad negativa | Sí | Intento de commit con un secreto de ejemplo |
| Arquitectura | No | No hay código de producto |
| Contrato | No | No hay contrato |

## 8. Decisiones
- **Un repositorio de backend:** contiene los tres servicios, el gateway, la infraestructura, los scripts de base de datos y las pruebas, y cada servicio se construye y despliega por separado.
- **Flujo con `develop`:** permite integrar y probar antes de tocar `main`.
- **Commits en inglés y documentación en español.**
- **La revisión del líder técnico es la obligatoria; la de pares, opcional (2026-11-11):** esperar el comentario de otro integrante frenaba el flujo y el equipo prefiere enfocarse en sus tareas; la revisión del líder técnico, con el reporte de la pre-revisión del agente, cumple «revisión de otra persona» de la constitución para el PR de cualquier integrante. Antes: **una** revisión de otra persona obligatoria.
- **Una revisión obligatoria, la segunda opcional (2026-11-04, reemplazada por la anterior):** pedir dos revisiones frenaba el flujo con cuatro personas; con una basta para detectar lo importante y la segunda queda a criterio de quien abre el PR. El líder técnico puede aprobar sus propios cambios para no bloquear lo urgente; queda el rastro en el PR.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-12
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
