# Spec 013 — Almacenamiento de archivos, CDN y moderación

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir cómo se suben, guardan y sirven las imágenes y cómo se revisa el contenido antes de guardarlo, sin implementar aún funciones que lo usen.

## 2. Alcance
**Dentro**
- Blob Storage con contenedores público y privado separados.
- Subida directa con enlaces temporales firmados.
- Reducción y limpieza de metadatos de las imágenes.
- CDN para imágenes públicas.
- Cliente de moderación compartido para texto e imágenes.

**Fuera**
- Las pantallas y flujos que suben fotos.
- Verificación de identidad: INE y selfie (consentimiento expreso, contenedor privado con llave propia, borrado a los 7 días) siguen la constitución del equipo (§6) y necesitan su propia spec.

## 3. Requisitos
- **R-01:** Las imágenes públicas y las privadas viven en contenedores de Blob separados.
- **R-02:** La subida es directa a Blob con un enlace firmado de 5 minutos; el servidor no recibe el archivo.
- **R-03:** Al subir, se quitan los metadatos de ubicación y se reduce el tamaño.
- **R-04:** Las imágenes públicas se sirven por la CDN; las privadas nunca pasan por ella y se entregan con enlaces temporales.
- **R-05:** Todo texto e imagen que sube una persona se envía a la API de moderación antes de guardarse; si se marca como inapropiado o la API no responde, no se guarda ni se publica.
- **R-06:** Un cliente de moderación compartido lo usa desde cada servicio; la API es Azure AI Content Safety.
- **R-07:** No se admite video.

## 4. Criterios de aceptación
- **CA-01:** Dada una imagen con coordenadas en sus metadatos, cuando se sube, entonces el archivo guardado no las tiene.
- **CA-02:** Dado un enlace de subida, cuando pasan 5 minutos, entonces deja de funcionar.
- **CA-03:** Dada una imagen privada, cuando se pide por la CDN, entonces no está disponible.
- **CA-04:** Dado un contenido marcado como inapropiado, cuando se intenta guardar, entonces se rechaza con un aviso claro.
- **CA-05:** Dado el servicio de moderación caído, cuando se sube contenido, entonces no se publica.

## 5. Contrato
Fija el formato de los enlaces firmados (vigencia de 5 minutos), el aviso de contenido rechazado y la interfaz del cliente de moderación en `Knock.Contracts`.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Fuga de ubicación por los metadatos de la foto | Se eliminan los metadatos al subir |
| Acceso a fotos privadas | Contenedor privado y separado, enlaces de 5 minutos y registro de cada acceso |
| Contenido inapropiado publicado | Moderación previa; si la API falla, se falla cerrado |
| Archivos maliciosos o demasiado grandes | Validación de tipo y tamaño antes de emitir el enlace |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Limpieza de metadatos y validación de tipo y tamaño |
| Integración | Sí | Blob (emulador) y un moderador falso |
| Seguridad negativa | Sí | Enlace vencido, imagen privada por la CDN y archivo malicioso |
| Contrato | Sí | Enlaces firmados y avisos en el OpenAPI |
| Arquitectura | No | Se cubre en la spec 001 |

## 8. Decisiones
- **Subida directa a Blob:** el servidor no carga con los archivos.
- **CDN solo para lo público:** acerca las imágenes a quien las consulta sin exponer lo privado.
- **Moderación que falla cerrada:** es preferible no publicar a dejar pasar contenido sin revisar.
- **Moderador falso en las pruebas de carga:** evita agotar la cuota gratuita.

## 9. Riesgos y dudas abiertas
- La cuota mensual gratuita de moderación es limitada.
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
