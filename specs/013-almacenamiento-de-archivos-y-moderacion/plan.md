# Plan 013 — Almacenamiento de archivos, CDN y moderación

> Parte de `spec.md` (estado Borrador). Este plan se confirma cuando la spec esté Aprobada.

## Enfoque
1. Crear los contenedores y la CDN en Terraform.
2. Implementar el servicio de enlaces firmados.
3. Implementar la limpieza y reducción de imágenes.
4. Implementar el cliente de moderación compartido.

## Archivos que se crean o cambian
- `src/Knock.Contracts/Moderation/`
- `src/<Servicio>/Common/Media/`
- `deploy/modules/shared/storage.tf`

## Dependencias
- Azure Blob Storage
- Azure CDN
- Azure AI Content Safety

## Decisiones de la spec que este plan sigue
- **Subida directa a Blob:** el servidor no carga con los archivos.
- **CDN solo para lo público:** acerca las imágenes a quien las consulta sin exponer lo privado.
- **Moderación que falla cerrada:** es preferible no publicar a dejar pasar contenido sin revisar.
- **Moderador falso en las pruebas de carga:** evita agotar la cuota gratuita.

## Verificación
- Cada criterio de aceptación de `spec.md` se comprueba con una prueba o un comando antes de dar el cambio por terminado.
- Se ejecutan los comandos de `AGENTS.md` y se actualiza `Memoria.md`.
