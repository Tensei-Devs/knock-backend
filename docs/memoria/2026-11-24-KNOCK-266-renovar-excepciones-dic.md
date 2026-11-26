# Excepciones de seguridad que vencían del 2 al 4 de diciembre: retiradas y renovadas

## Qué cambió
- Retiradas con evidencia: EXC-022 (no existe ya la rama de Media en `ModuleSurfaceTests`; el módulo expone `IMediaApi` desde KNOCK-185 y lo usa Asistente) y EXC-026 (Trivy sobre `mcr.microsoft.com/dotnet/aspnet:11.0`, reconstruida el 2026-11-24, da 0 hallazgos HIGH/CRITICAL sin archivo de exclusiones; `usr/bin/pebble` ya no marca nada). Se quitó CVE-2026-97054 de `.trivyignore`.
- Renovadas hasta 2027-01-15, cada una con plan: EXC-006 (las pruebas de arranque siguen usando `IStartupValidator`), EXC-019, EXC-023 y EXC-024 (los commits de las huellas siguen en 68 ramas remotas y no están en `sprint-05`).
- `.gitleaksignore` no se tocó.

## Por qué
- Antes del 2026-12-02 `check_exceptions.py` habría puesto el CI en rojo. Se retira lo que ya no hace falta y solo se renueva lo que se verificó que sigue cubriendo algo real.

## A evitar
- Renovar sin volver a comprobar: para EXC-019/023/024 se retira cuando `git branch -r --contains <commit>` no devuelva ramas; para EXC-006 cuando las cuatro clases de prueba usen `IAsyncStartupValidator`.
- Quitar una huella de `.gitleaksignore` mientras su commit siga en alguna rama.
