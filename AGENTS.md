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
