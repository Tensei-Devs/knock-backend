# Rulesets básicos

Reglas mínimas de GitHub para este repositorio. El profesor compartió capturas de su ruleset «Main Proteccion» (2026-11-03) y `main.json` se alineó con ellas. `develop` y las etiquetas son básicos del equipo.

| Archivo | Qué protege | Reglas |
|---|---|---|
| `main.json` | Rama `main` (lista para publicar) | No se borra, no se reescribe la historia (sin force push) y todo cambio entra por pull request con 1 aprobación, hilos resueltos y aprobaciones descartadas si hay commits nuevos |
| `develop.json` | Rama `develop` (integración) | Igual que `main` |
| `etiquetas-de-version.json` | Etiquetas `v*` (versiones `X.Y.Z`) | No se borran ni se mueven |

El rol de administrador del repositorio puede saltarse las reglas (para emergencias y para el líder del proyecto).

## Revisión de pares (2026-11-04)
Los rulesets piden **1 aprobación**; la segunda revisión es opcional. Para no frenar el flujo no se descartan las aprobaciones con commits nuevos ni se obliga a resolver todos los hilos (igual que el ruleset del profesor). El Tech Lead puede aprobar sus propios cambios mediante la excepción de administrador, y debe anotar el motivo en el PR.

## Cómo aplicarlos
1. En GitHub: **Settings → Rules → Rulesets → New ruleset → Import a ruleset**.
2. Elegir cada archivo `.json` de esta carpeta y crear el ruleset.

## Qué trae `main.json` frente al ruleset del profesor
- Igual: no borrar, bloquear force push, pull request con 1 aprobación, métodos de fusión Merge/Squash/Rebase, regla de comprobaciones obligatorias (sin ninguna agregada todavía) y revisión automática de Copilot.
- Más estricto que el profesor: aprobaciones descartadas con commits nuevos e hilos resueltos antes de fusionar.
- Distinto: el profesor deja la lista de excepciones vacía; aquí el rol de administrador puede saltarse las reglas (decisión del líder).
- Sin copiar (no está en el JSON): «Require an additional approval for unattributed Copilot pull requests»; se marca en la interfaz o al importar el JSON exportado por el profesor.

## Pendiente
- **Comprobaciones obligatorias del CI** (`required_status_checks`): hoy el pipeline existe y el trabajo que las reúne se llama `gate`; falta agregarlo al ruleset cuando se puedan hacer cumplir.
- **Plan de GitHub:** los repositorios están en una organización con plan gratuito y son privados; en ese caso GitHub no hace cumplir los rulesets ni la protección de ramas (la API responde 403). Opciones: hacerlos públicos o pasar la organización a un plan de pago. Mientras tanto:
  - el CI revisa el nombre de la rama, el formato de los commits y la plantilla del PR en el trabajo `pr-meta`, que se activa con la variable del repositorio `ENFORCE_BRANCH_RULES=true`;
  - la revisión de otra persona (una, obligatoria) es regla del equipo, no una restricción de GitHub (CA-02 de la spec 004 solo se cumple con un plan que lo permita);
  - cuando el Tech Lead aprueba su propio PR, anota el motivo en el PR (CA-06).
- **Revisión de código propietaria** (`CODEOWNERS`): el archivo existe en `.github/CODEOWNERS`; solo bloquea el merge si se activa «Require review from Code Owners», que tampoco aplica en repositorios privados gratuitos.
