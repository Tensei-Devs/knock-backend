# Knock · Backend

Knock conecta a personas con técnicos de confianza (mecánica, plomería, electricidad) de su colonia, empezando por una ciudad piloto. Este repositorio es el **backend**: los servicios que usan la app móvil y el panel web, su infraestructura en la nube y sus bases de datos. La descripción completa del proyecto está en `docs/descripcion-del-proyecto.md`.

Este repositorio se construye con **Spec Driven Development (SDD)**. En esta etapa contiene solo la base metodológica y las specs de arquitectura, estructura e infraestructura; todavía no hay código y las tareas no se han ejecutado.

## Contenido
- `constitution.md`: reglas no negociables del proyecto.
- `AGENTS.md`: manual para agentes de IA y personas.
- `Memoria.md`: memoria del proyecto.
- `.opencode/`: subagentes y skills para opencode; `opencode.json` carga la constitución y la memoria como contexto.
- `docs/descripcion-del-proyecto.md`: descripción del proyecto en palabras comunes; es la fuente de la que salen la constitución y las specs.
- `specs/`: una carpeta por spec con `spec.md`, `plan.md` y `task.md`.

## Specs de la base
| Spec | Tema | Estado |
|---|---|---|
| [001-arquitectura-de-microservicios-y-puerta-de-entrada](specs/001-arquitectura-de-microservicios-y-puerta-de-entrada/spec.md) | Arquitectura de microservicios y puerta de entrada | Borrador |
| [002-estructura-interna-de-cada-servicio-y-estilo-de-la-api](specs/002-estructura-interna-de-cada-servicio-y-estilo-de-la-api/spec.md) | Estructura interna de cada servicio y estilo de la API | Borrador |
| [003-bases-de-datos-mongodb-y-redis](specs/003-bases-de-datos-mongodb-y-redis/spec.md) | Bases de datos: MongoDB y Redis | Borrador |
| [004-estructura-del-repositorio-y-flujo-de-trabajo](specs/004-estructura-del-repositorio-y-flujo-de-trabajo/spec.md) | Estructura del repositorio y flujo de trabajo | Borrador |
| [005-infraestructura-en-azure-con-terraform](specs/005-infraestructura-en-azure-con-terraform/spec.md) | Infraestructura en Azure con Terraform | Borrador |
| [006-escalabilidad-balanceo-y-salud-de-los-servicios](specs/006-escalabilidad-balanceo-y-salud-de-los-servicios/spec.md) | Escalabilidad, balanceo y salud de los servicios | Borrador |
| [007-integracion-continua-y-despliegue](specs/007-integracion-continua-y-despliegue/spec.md) | Integración continua y despliegue | Borrador |
| [008-calidad-pruebas-y-estandares](specs/008-calidad-pruebas-y-estandares/spec.md) | Calidad, pruebas y estándares | Borrador |
| [009-seguridad-base-y-configuracion](specs/009-seguridad-base-y-configuracion/spec.md) | Seguridad base y configuración | Borrador |
| [010-cache-en-tres-niveles-y-paginacion](specs/010-cache-en-tres-niveles-y-paginacion/spec.md) | Caché en tres niveles y paginación | Borrador |
| [011-observabilidad-y-registro](specs/011-observabilidad-y-registro/spec.md) | Observabilidad y registro | Borrador |
| [012-versionamiento-por-puntos](specs/012-versionamiento-por-puntos/spec.md) | Versionamiento por puntos | Borrador |
| [013-almacenamiento-de-archivos-y-moderacion](specs/013-almacenamiento-de-archivos-y-moderacion/spec.md) | Almacenamiento de archivos, CDN y moderación | Borrador |
| [014-entrega-del-proyecto-y-reproducibilidad](specs/014-entrega-del-proyecto-y-reproducibilidad/spec.md) | Entrega del proyecto y reproducibilidad | Borrador |

## Cómo usarlo con opencode
```bash
opencode            # en la raíz del repositorio; lee AGENTS.md y opencode.json
```
Ejemplos: `@spec-writer refina la spec 001`, `@plan-writer escribe el plan de la spec 001`, `@constitution-reviewer revisa la carpeta specs/`.
