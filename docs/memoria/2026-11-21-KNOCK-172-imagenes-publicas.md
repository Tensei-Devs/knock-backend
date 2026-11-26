# Imágenes públicas: AlmacenBlob, límites, endpoints, enlaces privados y ruta del gateway (spec 032, T-04 a T-09)

## Qué cambió
- `AlmacenBlob` (SAS de delegación de usuario `cw` de un blob, llave reutilizada mientras le quede más de una hora, propiedades, descarga con ETag y tope, publicación sin sobrescritura con caché inmutable y borrado) con la interfaz `IAlmacenBlob`; `AlmacenNoDisponibleException` se traduce a 503 `storage_unavailable`.
- `LimitesImagen`: 20 enlaces y 30 confirmaciones por hora (`img:subidas:{sub}` e `img:confirmaciones:{sub}`, ventana fija en Redis) y 10 pendientes vigentes; si Redis falla se usa un contador por instancia.
- `SubidaImagenService`, `ImagenesController` (`POST /api/v1/imagenes/subidas` y `/{id}/confirmar`), validador, `SubidasImagenStore` (colección `subidas_imagen`) y registro en el arranque; base `Mongo:Databases:Media` y `Imagenes:BaseUrlPublica` en Development.
- `IEnlacesPrivados` con SAS de lectura de 5 minutos, `no-store` forzado, solo el contenedor `verificaciones` y registro en `accesos_privados` con HMAC del `sub`.
- Rutas del gateway y filas de `docs/endpoints.md`. El OpenAPI regenerado y su prueba de contrato van en la rama `feature/KNOCK-172-openapi-AUTORIZACION`.

## Por qué
La spec 032 (R-02, R-05 a R-16) exige SAS de delegación de usuario, moderación con falla cerrada, publicación por huella y límites compartidos entre réplicas. Separar `IAlmacenBlob` permite probar el servicio y el canal HTTP sin almacenamiento.

## A evitar
- No firmar SAS con llave de cuenta ni con permisos distintos de `cw` (subida) o `r` (lectura de verificaciones).
- No registrar `urlSubida`, `sig=`, el `sub` ni el nombre del blob de cuarentena.
- No llamar `AlmacenBlob` desde el controller ni abrir el paso sin límite si Redis cae.

## PARA VALIDACIÓN DE JOSÉ
- Sin el paquete Azure.Identity el código exige un `TokenCredential` registrado cuando se usan `Imagenes:CuentaPrivadaUrl` y `CuentaPublicaUrl`; agregar `DefaultAzureCredential` requiere el paquete (con la 1.17.1 en caché choca con Azure.Core 1.63) y restaurar en línea.
- `Imagenes:BaseUrlPublica` pasa a ser obligatoria al arrancar Mercado en cualquier entorno: el despliegue debe llenarla antes de fusionar.