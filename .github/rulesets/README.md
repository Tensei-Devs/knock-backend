# Rulesets básicos

Reglas mínimas de GitHub para este repositorio. El profesor dijo que compartiría una imagen con los *rulesets* sugeridos; **no se recibió**, así que estos son los básicos que el equipo definió. Cuando llegue la imagen se comparan y se ajustan.

| Archivo | Qué protege | Reglas |
|---|---|---|
| `main.json` | Rama `main` (lista para publicar) | No se borra, no se reescribe la historia (sin force push) y todo cambio entra por pull request con 1 aprobación, hilos resueltos y aprobaciones descartadas si hay commits nuevos |
| `develop.json` | Rama `develop` (integración) | Igual que `main` |
| `etiquetas-de-version.json` | Etiquetas `v*` (versiones `X.Y.Z`) | No se borran ni se mueven |

El rol de administrador del repositorio puede saltarse las reglas (para emergencias y para el líder del proyecto).

## Cómo aplicarlos
1. En GitHub: **Settings → Rules → Rulesets → New ruleset → Import a ruleset**.
2. Elegir cada archivo `.json` de esta carpeta y crear el ruleset.

## Pendiente
- **Comprobaciones obligatorias del CI** (`required_status_checks`): se agregan cuando exista el spec de CI/CD y sus trabajos tengan nombre. Hoy no hay pipeline.
- **Plan de GitHub:** los repositorios están en una organización con plan gratuito y son privados; en ese caso GitHub no hace cumplir los rulesets en repositorios privados. Opciones: hacerlos públicos o pasar la organización a un plan de pago.
- **Revisión de código propietaria** (`CODEOWNERS`): se activa cuando haya un archivo `CODEOWNERS`.
