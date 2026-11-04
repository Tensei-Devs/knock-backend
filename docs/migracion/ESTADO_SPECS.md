# Estado de las specs (2026-11-03)

**Cómo leerlo.** *Estado* es lo que dice cada `spec.md`: Borrador → Aprobada (una persona; habilita el código) → Firmada (tras el merge) → Implementada. *Con prueba* cuenta los casos TC de la spec que aparecen nombrados en alguna prueba del repositorio; **no significa que la prueba pase ni que el código esté completo**. Las tareas de proceso o configuración (repositorio, ramas, Jira, SDD, Azure, CI) se verifican con comandos, no con pruebas, y por eso salen «sin pruebas».

## Specs del equipo (`specs/_legacy`)
Resumen por estado: 21 Aprobada, 3 Firmada, 2 Reemplazada.

| Spec | Estado | Casos | Con prueba | Avance en pruebas |
|---|---|---|---|---|
| HU-001-entrar-con-celular-y-codigo-sms | Aprobada | 25 | 0 | sin pruebas |
| HU-009-zona-por-ubicacion-o-codigo-postal | Aprobada | 6 | 0 | sin pruebas |
| TT-API-01-proyecto-aspnet-core-10-base | Aprobada | 16 | 14 | parcial (14/16) |
| TT-API-02-spike-token-firebase-sesion-jwt | Aprobada | 17 | 14 | parcial (14/17) |
| TT-API-03-middlewares-errores-problemdetails-serilog | Firmada | 18 | 16 | parcial (16/18) |
| TT-API-05-servicio-de-fotos-privadas-blob-sas | Aprobada | 3 | 3 | completo |
| TT-API-06-pipeline-de-seguridad-headers-hsts-cors | Aprobada | 20 | 18 | parcial (18/20) |
| TT-API-07-endpoint-colonia-por-punto | Aprobada | 18 | 16 | parcial (16/18) |
| TT-API-12-sub-v8-con-sha-256 | Aprobada | 10 | 7 | parcial (7/10) |
| TT-AZ-01-cuenta-de-azure-grupo-de-recursos-presup | Firmada | 3 | 0 | sin pruebas |
| TT-AZ-02-infraestructura-como-codigo-terraform | Aprobada | 12 | 0 | sin pruebas |
| TT-AZ-05-estado-remoto-de-terraform | Aprobada | 5 | 0 | sin pruebas |
| TT-BD-01-postgresql-postgis-docker-compose | Reemplazada | 5 | 0 | — |
| TT-BD-02-catalogo-base-oficios-colonias | Reemplazada | 9 | 0 | — |
| TT-BD-03-semillas-ficticias-mongodb | Aprobada | 11 | 0 | sin pruebas |
| TT-BD-05-spike-documentdb-vs-atlas | Firmada | 9 | 7 | parcial (7/9) |
| TT-BD-06-migracion-catalogo-mongodb | Aprobada | 22 | 20 | parcial (20/22) |
| TT-CI-01-github-actions-en-cada-pr-job-branch-nam | Aprobada | 4 | 0 | sin pruebas |
| TT-FB-01-proyecto-de-firebase-autenticacion-por-t | Aprobada | 4 | 0 | sin pruebas |
| TT-OBS-01-observabilidad-application-insights-y-sentry | Aprobada | 22 | 14 | parcial (14/22) |
| TT-PRU-01-endpoint-get-api-v1-oficios | Aprobada | 7 | 6 | parcial (6/7) |
| TT-REPO-01-crear-repositorios-knock-backend-y-frontend | Aprobada | 3 | 0 | sin pruebas |
| TT-REPO-02-ramas-main-release-develop-y-sprint-00-c | Aprobada | 4 | 0 | sin pruebas |
| TT-REPO-03-conectar-github-con-jira-clave-knock-par | Aprobada | 2 | 0 | sin pruebas |
| TT-SDD-01-constitucion-agents-md-y-skills-base-par | Aprobada | 5 | 0 | sin pruebas |
| TT-STD-01-reglas-de-codificacion-editorconfig-anal | Aprobada | 4 | 0 | sin pruebas |

## Specs de Demo Base (`specs/NNN-tema`)
Todas están en **Borrador**: ninguna está aprobada ni firmada, y ninguna tiene código propio que la implemente. El código que existe (Hosting y Catalog en `Knock.Market.Api`) viene de las specs del equipo de arriba.
