# AGENTS.md — Knock · Backend

> Manual operativo para el agente de IA y para las personas del equipo. Lee primero `constitution.md` y `Memoria.md`; si algo aquí la contradice, manda la constitución.

## Proyecto
Knock conecta a personas con técnicos de confianza (mecánica, plomería, electricidad) de su colonia, empezando por una ciudad piloto. Este repositorio es el **backend**: los servicios que usan la app móvil y el panel web, su infraestructura en la nube y sus bases de datos. La descripción completa del proyecto está en `docs/descripcion-del-proyecto.md`.

## Cómo trabajamos (SDD)
1. **Spec** (`specs/<NNN-nombre>/spec.md`): qué se construye y por qué, con alcance, requisitos, criterios de aceptación, contrato, amenazas y plan de pruebas.
2. **Plan** (`plan.md`): cómo se construye: enfoque, archivos, dependencias y orden.
3. **Tareas** (`task.md`): pasos pequeños y verificables, cada uno con el requisito y el criterio que cubre.
4. **Implementación**: solo cuando la spec está **Aprobada** y solo lo que dicen las tareas.
5. **Verificación y memoria**: se ejecutan las pruebas y se actualiza `Memoria.md`.

## Comandos
```bash
dotnet build Knock.slnx -c Release
dotnet test Knock.slnx -c Release
dotnet format Knock.slnx --verify-no-changes
docker compose up
```

## Reglas para el agente
- No escribas código si la spec no está Aprobada. Esta primera etapa es solo de especificación: **no ejecutes las tareas**.
- No inventes requisitos: lo que no sepas va a la sección «Riesgos y dudas abiertas» de la spec.
- Haz cambios pequeños y explica qué cambiaste y por qué.
- Nunca incluyas secretos, claves ni datos personales reales.
- No agregues dependencias sin evaluarlas; regístralas en `Memoria.md`.
- No apruebes specs, no fusiones y no operes sobre producción: eso lo hace una persona.
- Declara en cada pull request la sección «Uso de IA» (va en la plantilla del PR, no dentro de las specs).
- Si algo no se pudo ejecutar o verificar, dilo claramente.

## Subagentes (`.opencode/agents/`)
| Agente | Para qué sirve |
|---|---|
| `spec-writer` | Redacta o refina un `spec.md` a partir de la constitución |
| `plan-writer` | Redacta el `plan.md` de una spec aprobada |
| `task-writer` | Divide el plan en `task.md` con pasos verificables |
| `constitution-reviewer` | Revisa que specs, planes y tareas sean coherentes con la constitución (solo lectura) |
| `implementer` | Implementa las tareas de una spec aprobada (no se usa en esta etapa) |

## Skills (`.opencode/skills/`)
`sdd-workflow`, `writing-specs`, `writing-plans`, `writing-tasks`, `security-basics` y las de este repositorio: `microservices-gateway`, `mongodb-redis-data-access`, `terraform-azure`.

## Dónde está cada cosa
- Reglas: `constitution.md` · Memoria del proyecto: `Memoria.md` · Specs: `specs/` · Descripción del proyecto en palabras comunes (de ella salen la constitución y las specs): `docs/descripcion-del-proyecto.md`

<!-- skills-index:start -->
## 12. Índice de skills, agentes y reglas (generado)

> Si tu herramienta no carga skills por sí sola, abre el `SKILL.md` indicado cuando la tarea coincida con su descripción.

| Skill | Cuándo usarla | Archivo |
|---|---|---|

| Agente | Para qué | Archivo |
|---|---|---|
| `api-builder` | Implementa la capa [Back] de un issue aprobado de knock-backend (contrato, dominio, servicio, controller, eventos y pruebas) dentro de su módulo, siguiendo story-workflow. | `.agents/agents/api-builder.md` |
| `db-builder` | Implementa la capa [BD] de un issue aprobado de knock-backend (modelo, migraciones EF Core, semillas, catálogo SEPOMEX/PostGIS, scripts y respaldos) con sus pruebas de base de datos. | `.agents/agents/db-builder.md` |
| `docs-keeper` | Mantiene vivos los Markdown de Knock (README, docs, índice de specs y Memoria) al cerrar un issue, sin tocar código. | `.agents/agents/docs-keeper.md` |
| `security-reviewer` | Audita sin editar la seguridad y privacidad del cambio de un issue de Knock (OWASP Top 10 y API Top 10, amenazas de la spec, datos sensibles, cadena de suministro) y entrega PASS/FAIL; un FAIL bloquea. | `.agents/agents/security-reviewer.md` |
| `spec-writer` | Refina el borrador de spec de un issue de Knock (HU, TT o BUG) - clave de Jira, verificación por caso, alcance por capa, contrato, amenazas y plan de pruebas - sin tocar código. Úsalo antes de implementar. | `.agents/agents/spec-writer.md` |
| `traceability-clerk` | Reporta sin editar el drift entre la spec, el código y las pruebas de un issue de Knock antes del PR. | `.agents/agents/traceability-clerk.md` |

| Regla | Aplica a | Archivo |
|---|---|---|
| `backend-csharp` | `src/**/*.cs`, `src/**/*.csproj` | `.agents/rules/backend-csharp.md` |
| `database` | `db/**`, `src/**/Migrations/**`, `src/**/Data/**` | `.agents/rules/database.md` |
| `specs` | `specs/**` | `.agents/rules/specs.md` |
| `tests-backend` | `tests/**` | `.agents/rules/tests-backend.md` |
<!-- skills-index:end -->
