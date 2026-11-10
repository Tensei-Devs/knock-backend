# Spec 013 — Almacenamiento de archivos, entrega con caché y moderación

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir cómo se suben, guardan y sirven las imágenes y cómo se revisa el contenido antes de guardarlo, sin implementar aún funciones que lo usen.

## 2. Alcance
**Dentro**
- Blob Storage con dos cuentas de almacenamiento separadas: la pública (solo imágenes ya procesadas) y la privada (cuarentena, verificación y respaldos), según el ADR-007.
- Subida directa con enlaces temporales firmados.
- Reducción y limpieza de metadatos de las imágenes.
- Entrega de las imágenes públicas directo desde el almacenamiento, con caché de larga duración e inmutable y una dirección base configurable. No se crea ninguna CDN de pago (ADR-007).
- Cliente de moderación compartido para texto e imágenes.

**Fuera**
- Las pantallas y flujos que suben fotos.
- Crear un perfil de Azure Front Door, Azure CDN o una CDN de otro proveedor: la decisión y su costo están en el ADR-007. Si algún día se agrega una, solo cambia el valor de la dirección base.
- Verificación de identidad: INE y selfie (consentimiento expreso, contenedor privado con llave propia, borrado a los 7 días) siguen la constitución del equipo (§6) y necesitan su propia spec.

## 3. Requisitos
- **R-01:** Las imágenes públicas y las privadas viven en cuentas de almacenamiento separadas. La cuenta privada tiene deshabilitada la lectura anónima a nivel de cuenta, de modo que ningún contenedor suyo puede volverse público.
- **R-02:** La subida es directa a Blob con un enlace firmado de 5 minutos (SAS de delegación de usuario, permisos de crear y escribir sobre un solo blob); el servidor no recibe el archivo.
- **R-03:** Al confirmar una subida se valida que sea JPEG, PNG o WebP (por sus bytes iniciales; el tipo declarado no basta) y que no pase de 5 MB (5 242 880 bytes); se quitan todos los metadatos, incluida la ubicación; se reduce el tamaño y se recodifica a JPEG. Los límites numéricos de decodificación son los de la spec 032 (R-07 a R-09).
- **R-04:** Las imágenes públicas se sirven directo desde el contenedor público, con `Cache-Control: public, max-age=31536000, immutable` y una dirección base configurable (`Imagenes:BaseUrlPublica`); no hay CDN de pago (ADR-007). Las privadas nunca se sirven por esa dirección, no admiten lectura anónima y se entregan con enlaces temporales de 5 minutos.
- **R-05:** Todo texto e imagen que sube una persona se envía a la API de moderación antes de guardarse; si se marca como inapropiado, no se guarda ni se publica y se rechaza. Si la API no responde, tampoco se publica (falla cerrada), pero la subida no se descarta: se responde 503 reintentable con `Retry-After` y la persona puede confirmar de nuevo hasta que venza su plazo.
- **R-06:** Un cliente de moderación compartido lo usa desde cada servicio; la API es Azure AI Content Safety.
- **R-07:** No se admite video.
- **R-08:** El registro de cada subida (spec 032, colección `subidas_imagen`) vence a los 7 días **solo si la subida quedó pendiente o rechazada**. El de una subida publicada se conserva mientras exista su imagen, porque es lo que permite confirmar otra vez sin reprocesar.

## 4. Criterios de aceptación
- **CA-01:** Dada una imagen con coordenadas en sus metadatos, cuando se sube, entonces el archivo guardado no las tiene.
- **CA-02:** Dado un enlace de subida, cuando pasan 5 minutos, entonces deja de funcionar.
- **CA-03:** Dada una imagen de la cuenta privada, cuando se pide sin credenciales o con la dirección base pública, entonces el almacenamiento la rechaza.
- **CA-04:** Dado un contenido marcado como inapropiado, cuando se intenta guardar, entonces se rechaza con un aviso claro.
- **CA-05:** Dado el servicio de moderación caído, cuando se confirma una subida, entonces no se publica nada, la respuesta es 503 con `Retry-After` y la misma subida se puede confirmar después con éxito.
- **CA-06:** Dada una subida de más de 5 MB o de un formato distinto de JPEG, PNG o WebP, cuando se confirma, entonces se rechaza con 422 y se borra el archivo subido.
- **CA-07:** Dados los registros de subidas, cuando pasan 7 días, entonces el de una subida pendiente o rechazada ya no existe y el de una publicada sigue existiendo.

## 5. Contrato
Fija el formato de los enlaces firmados (vigencia de 5 minutos), el aviso de contenido rechazado y la interfaz del cliente de moderación en `Knock.Contracts`. Las rutas de subida y confirmación, sus respuestas y sus códigos de error están en la spec 032.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Fuga de ubicación por los metadatos de la foto | Se eliminan los metadatos al subir |
| Acceso a fotos privadas | Cuenta privada separada y sin lectura anónima, enlaces de 5 minutos y registro de cada acceso |
| Contenido inapropiado publicado | Moderación previa; si la API falla, se falla cerrado |
| Archivos maliciosos o demasiado grandes | Validación de tipo por contenido y de tamaño (5 MB) al confirmar y antes de publicar |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Limpieza de metadatos y validación de tipo y tamaño |
| Integración | Sí | Blob (emulador), un moderador falso y el caso de moderación caída (503 reintentable) |
| Seguridad negativa | Sí | Enlace vencido, imagen privada pedida sin credenciales y archivo malicioso |
| Contrato | Sí | Enlaces firmados y avisos en el OpenAPI |
| Arquitectura | No | Se cubre en la spec 001 |

## 8. Decisiones
- **Subida directa a Blob:** el servidor no carga con los archivos.
- **Sin CDN de pago (ADR-007, opción D):** las imágenes públicas se sirven desde Blob con caché larga e inmutable y dirección base configurable; lo privado nunca pasa por ahí. Una CDN futura solo cambia esa dirección.
- **Dos cuentas de almacenamiento:** habilitar la lectura anónima de lo público no puede alcanzar lo privado.
- **Moderación que falla cerrada:** es preferible no publicar a dejar pasar contenido sin revisar.
- **Moderación caída = reintentable:** no se descarta una foto válida por una caída del moderador.
- **Moderador falso en las pruebas de carga:** evita agotar la cuota gratuita.

## 9. Riesgos y dudas abiertas
- La cuota mensual gratuita de moderación es limitada.
- El punto 15c de los lineamientos pide una CDN; con la opción D queda parcial y documentado (ADR-007).
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-04
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
