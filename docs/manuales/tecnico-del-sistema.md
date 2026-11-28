# Manual técnico del sistema

Guía para preparar un entorno local, verificar un cambio y entregar evidencia de CI.

## Desarrollo local

- **Backend.** Usa el SDK indicado en [global.json](../../global.json) y Docker para levantar las dependencias de [Compose local](../../deploy/docker-compose.local.yml). El gateway concentra las rutas públicas. Identidad, Mensajería y Mercado mantienen sus propios módulos. Consulta [la arquitectura](../arquitectura.md) y [el catálogo de endpoints](../endpoints.md).
- **Frontend.** En su repositorio, los README de `app/`, `panel/` y `landing/` indican cómo iniciar cada componente. Configura `KNOCK_API_URL` con el gateway del entorno de prueba para verificar el recorrido autenticado.
- **Datos.** Usa las semillas ficticias de [db/](../../db/) y un entorno local separado. Carga solo la configuración necesaria por entorno y mantén las credenciales fuera del código, los registros y las capturas.

## Comprobar el entorno

1. Usa una copia de trabajo aislada y la configuración local aprobada para Firebase, MongoDB, Redis y el almacenamiento de prueba. [Configuración de ejemplo](../../.env.example).
2. Comprueba la disponibilidad del gateway en `GET /health`. Usa `GET /health/ready` para verificar que las dependencias estén listas. Si el segundo responde 503, revisa el componente que declara el fallo y su configuración local. [Arquitectura](../arquitectura.md).
3. Abre el cliente con datos ficticios. Verifica una lectura autenticada antes de probar escrituras. Para el recorrido de fotos de solicitud, confirma que el entorno soporta el flujo privado de la spec. [Spec 062](../../specs/062-fotos-privadas-de-solicitud/spec.md).

## Verificar un cambio

1. Lee la spec y las tareas aprobadas del ticket. Confirma que el contrato y los clientes coincidan si cambia un endpoint.
2. Ejecuta las pruebas focalizadas y el análisis del componente. Para backend, usa `dotnet build Knock.slnx -c Release` y `dotnet test Knock.slnx -c Release` con el entorno local documentado. Para el frontend, consulta los comandos de verificación de cada componente.
3. Ejecuta la trazabilidad de la spec y comprueba el mapa de código. Adjunta los resultados al PR y revisa el gate de CI antes de la revisión humana. [Guía SDD](../SDD_GUIA.md) · [Calidad y DevSecOps](../QA_DEVSECOPS.md).

## Entregar el cambio

Publica la rama y abre un PR hacia `sprint-05` con la clave de Jira, los comandos ejecutados y el resultado de cada criterio. Cuando falle CI, revisa el paso y el SHA de esa ejecución. Registra los fallos pendientes y conserva los controles de seguridad activos.

Las capturas del entorno local y del CI se agregan después de publicar con datos ficticios y sin credenciales.
