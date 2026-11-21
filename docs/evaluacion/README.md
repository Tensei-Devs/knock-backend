# Evidencia de evaluación del backend

Esta carpeta junta, en un solo lugar, lo que un evaluador necesita para comprobar las respuestas sobre el backend. Los números **salen de un script**, no se escriben a mano.

| Archivo | Qué es |
|---|---|
| `generar_evidencia.py` | Generador (solo lectura, sin dependencias). `python docs/evaluacion/generar_evidencia.py [--gh] [--sonar]` |
| `generado/cifras.json` y `generado/cifras.md` | Salida del generador: endpoints, pruebas (recuento estático), agentes y skills, memoria, specs, réplicas por entorno y, opcionalmente, GitHub y el quality gate de SonarCloud, cada cifra con su método, fecha y SHA |
| `brechas-conocidas.md` | Lo que **no** está o no actúa hoy, dicho sin adornos |

## Cómo regenerar
```
python docs/evaluacion/generar_evidencia.py --sonar          # cifras del repositorio y quality gate de SonarCloud
python docs/evaluacion/generar_evidencia.py --gh --sonar     # además, estado de GitHub (requiere gh con sesión)
```
Cada cifra dice cómo se obtuvo: **estático** (se cuenta en archivos; no es una corrida de pruebas) o **consulta** (a GitHub o a SonarCloud). Las cifras de una corrida real (casos ejecutados, cobertura) salen del artefacto `api-reports` y del log de `ci-pr`.

## SonarCloud
El generador lee el estado del quality gate de la rama principal por la API pública (`api/qualitygates/project_status`), que no pide token en proyectos públicos. Mientras no exista un análisis, el estado es `NONE`. Si la API no responde, el campo `quality_gate_manual` queda vacío para que una persona lo capture.

## Qué falta (Fase 0 del plan)
Esta es la Fase 0 del backend. Quedan la matriz OWASP Top 10 control→prueba, la tabla endpoint→pruebas y reconciliar `docs/EVALUACION.md` con `generado/cifras.md`; el generador del móvil vive en el repositorio del frontend.
