# Manual técnico del sistema

Borrador para revisión del equipo sobre `sprint-05`.

## Desarrollo local

- **Backend.** Usa el SDK indicado en [global.json](../../global.json) y el entorno aislado descrito en [la guía de despliegue local](../../deploy/README.md). El gateway concentra las rutas públicas. Identidad, Mensajería y Mercado mantienen sus propios módulos. Consulta [la arquitectura](../arquitectura.md) y [el catálogo de endpoints](../endpoints.md).
- **Frontend.** En su repositorio, los README de `app/`, `panel/` y `landing/` indican cómo iniciar cada componente. Configura `KNOCK_API_URL` con el gateway del entorno de prueba para verificar el recorrido autenticado.
- **Datos.** Usa las semillas ficticias de [db/](../../db/) y un entorno local separado. Carga solo la configuración necesaria por entorno y mantén las credenciales fuera del código, los registros y las capturas.

## Verificar un cambio

1. Lee la spec y las tareas aprobadas del ticket. Confirma que el contrato y los clientes coincidan si cambia un endpoint.
2. Ejecuta las pruebas focalizadas y el análisis del componente. Para backend, usa `dotnet build Knock.slnx -c Release` y `dotnet test Knock.slnx -c Release` con el entorno local documentado. Para el frontend, consulta los comandos de verificación de cada componente.
3. Ejecuta la trazabilidad de la spec y comprueba el mapa de código. Adjunta los resultados al PR y revisa el gate de CI antes de la revisión humana. [Guía SDD](../SDD_GUIA.md) · [Calidad y DevSecOps](../QA_DEVSECOPS.md).

Capturas pendientes: ejecución local con semillas y CI de una rama de ejemplo sin información sensible.
