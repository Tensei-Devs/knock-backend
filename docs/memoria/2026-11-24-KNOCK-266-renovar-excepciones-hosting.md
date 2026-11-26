# Renovación de EXC-015, EXC-016, EXC-018 y EXC-021 hasta 2027-01-15

## Qué cambió
- `security/exceptions.yaml`: las cuatro excepciones de duplicación que vencían el 2026-12-04 (EXC-015, EXC-016, EXC-018) y el 2026-12-15 (EXC-021) pasan a vencer el 2027-01-15 (dentro del tope de 90 días desde su creación). Cada una dice qué pieza falta mover a `Knock.Hosting` y cuándo se retira.
- `specs/055.../task.md` (T-20) anota la renovación.

## Por qué
- La biblioteca técnica (spec 055) ya absorbió las piezas de las etapas 1 a 3, pero faltan `ProblemResponses`, CORS, `RequestBodyLimitMiddleware` y el resto de `Common/` y `Hosting/` de Identity (T-17 y T-19), y el método `Matches` de EXC-018 no está en el catálogo de la spec (R-04). Dejarlas vencer rompería `check_exceptions.py` en el CI.

## A evitar
- Renovar sin decir qué falta. Cada PR que mueve una pieza debe quitar sus marcadores `jscpd:ignore` y recortar el alcance de la excepción; al mover la última se retira.
- Otras excepciones con vencimiento el 2026-12-02 al 2026-12-04 (EXC-006, 019, 022, 023, 024, 026) no son de esta tarea.
