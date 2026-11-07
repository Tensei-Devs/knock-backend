# ADR-007 — CDN de las imágenes públicas

> **Estado:** propuesta, pendiente de aprobación del Tech Lead (consulta de documentación y tarifas: 2026-11-06).
> **Complementa:** la sección 10 de `docs/DECISION_TECNICA.md`, el [ADR-006](ADR-006-costos-y-ventana-de-evaluacion.md) y la spec 013.
> **Tarea:** TT-GES-10 (KNOCK-182) · Impacto de implementación: TT-API-10, TT-AZ-02, TT-CD-02.

## Contexto
Los lineamientos piden que las imágenes se sirvan desde una CDN de la nube (punto 15c de `docs/CUMPLIMIENTO_LINEAMIENTOS.md`), y la spec 013 (R-04) dice que las imágenes públicas, como los avatares y las fotos de trabajos terminados, van por la CDN, mientras que **las fotos de verificación son privadas y nunca pasan por ella**. `DECISION_TECNICA.md` nombraba «Front Door» como CDN, y el ADR-006 dejó para esta tarea el costo, porque Front Door con WAF no se despliega sin pasar por una decisión.

Hechos de la documentación (consulta del 2026-11-06):
- **Azure CDN Standard from Microsoft (classic)** se retira el **30 de septiembre de 2027** y **ya no admite crear perfiles**, dar de alta dominios nuevos ni certificados administrados; desde el 15 de agosto de 2025 no admite recursos nuevos. Microsoft indica migrar a Azure Front Door Standard o Premium ([preguntas frecuentes del retiro](https://learn.microsoft.com/en-us/azure/cdn/classic-cdn-retirement-faq)). Por tanto, **Azure CDN classic no es una opción para un perfil nuevo**.
- La única CDN de Microsoft que se puede crear hoy es **Azure Front Door Standard o Premium**. Su tarifa ([precios de Front Door](https://azure.microsoft.com/en-us/pricing/details/frontdoor/)): cuota base de **35 USD al mes** en Standard y **330 USD** en Premium (cobrada por hora de uso), más solicitudes (0.009 USD por cada 10 000 en Norteamérica y Europa, Standard) y transferencia de salida (0.083 USD por GB en Norteamérica y Europa, primeros 10 TB). La página **no lista nivel gratuito**.
- La lectura anónima de Blob Storage es un ajuste de la **cuenta** (`allowBlobPublicAccess`) y después del **contenedor**. Si la cuenta la deshabilita, ningún contenedor de esa cuenta puede ser público: el ajuste de la cuenta manda sobre el del contenedor ([documentación de Azure Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/anonymous-read-access-configure)).
- La salida de datos a internet de Azure incluye **100 GB gratis al mes**; después, 0.087 USD por GB en el primer tramo ([precios de ancho de banda](https://azure.microsoft.com/en-us/pricing/details/bandwidth/)).
- El almacenamiento Blob (Hot, LRS, West US) cuesta 0.0208 USD por GB al mes según la API de precios de Azure, consultada en `docs/evidencia/KNOCK-145-costo-cero.md`.
- Cloudflare publica un plan **Free de 0 USD al mes que incluye CDN** ([planes de Cloudflare](https://www.cloudflare.com/plans/network-cdn/)) y, por omisión, guarda en caché los archivos por extensión, entre ellas JPG, PNG, GIF, WEBP y SVG ([comportamiento de caché](https://developers.cloudflare.com/cache/concepts/default-cache-behavior/)).

## Opciones comparadas
| Opción | Se puede crear hoy | Costo | Cumple «CDN» | Riesgo para lo privado |
|---|---|---|---|---|
| A. Azure CDN Standard from Microsoft (classic) | **No** (creación de perfiles retirada) | — | — | — |
| B. Azure Front Door Standard | Sí | 35 USD al mes de base, más solicitudes y salida; sin nivel gratuito | Sí | Bajo si solo se asocia al origen público |
| C. Azure Front Door Premium | Sí | 330 USD al mes de base | Sí | Bajo |
| D. Blob público sin CDN, con `Cache-Control` largo | Sí | Almacenamiento más salida; 100 GB al mes gratis | No es CDN | Bajo si la cuenta pública es otra |
| E. Cloudflare Free delante del Blob público | Sí | 0 USD al mes (plan Free) | Sí, de otro proveedor | Bajo si la cuenta pública es otra |

## Decisión
1. **No se crea ningún perfil de CDN de pago.** Front Door Standard (opción B) es el único producto de Azure que se puede crear y cuesta 35 USD al mes aunque no tenga tráfico; la regla de costo cero del equipo lo descarta mientras no haya una decisión expresa del Tech Lead que autorice el gasto con el crédito. Front Door Premium (opción C) queda descartada por costo y porque su diferencia (WAF y Private Link) no la necesita una imagen pública.
2. **Solución vigente sin costo (opción D):** las imágenes públicas se sirven desde un contenedor de Blob de **lectura anónima a nivel Blob** (sin listado) con encabezado `Cache-Control` largo e inmutable por nombre de archivo (el nombre lleva el hash del contenido). La dirección base de las imágenes sale de configuración (`Perfil:HostsFoto` y su equivalente en los otros servicios), de modo que **poner una CDN delante después no cambia código ni datos**, solo el valor de esa dirección y los dominios permitidos.
3. **Camino sin costo hacia una CDN real (opción E):** Cloudflare Free delante del Blob público. Antes de adoptarlo hay que leer en su documentación las condiciones del plan (por ejemplo, si exige un dominio propio y sus límites de uso), porque este ADR no las verificó; si el equipo no cuenta con un dominio, la opción E no aplica.
4. **Las fotos de verificación son siempre privadas y nunca pasan por una CDN**, cualquiera que sea la opción: viven en una **cuenta de almacenamiento distinta** a la de las imágenes públicas, con `allowBlobPublicAccess = false` (el ajuste de la cuenta anula cualquier contenedor público), se entregan solo con enlaces firmados de 5 minutos y cada acceso se registra. Esto refuerza el R-01 de la spec 013 (contenedores separados) con una separación de cuenta: sin ella, habilitar la lectura anónima para las imágenes públicas la habilitaría en la misma cuenta que guarda las fotos privadas.
5. El punto 15c de los lineamientos queda **sin cumplirse a la letra** con la opción D; se cumple con la E o con la B si el Tech Lead autoriza el gasto. Esa decisión es suya.

## Costo
- Opción D: almacenamiento a 0.0208 USD por GB al mes (unos 0.21 USD por cada 10 GB) y salida gratis hasta 100 GB al mes; después, 0.087 USD por GB. Con las fotos reducidas de tamaño (R-03 de la spec 013), el uso del piloto queda por debajo de esos 100 GB.
- Opción E: 0 USD por la CDN; el almacenamiento y el origen cuestan igual que en D, y la salida del origen baja porque la CDN responde desde su caché.
- Opción B, solo como referencia para el líder: la cuota base, cobrada por hora de uso, equivale a unos 12.8 USD para la ventana de evaluación del 25 de noviembre al 5 de diciembre (35 USD × 11 días ÷ 30), más solicitudes y salida. Saldría del crédito de Azure for Students (ADR-006). **No se recomienda ni se despliega.**

## Consecuencias
- TT-API-10 no crea ningún recurso de CDN; implementa el contenedor público, el encabezado `Cache-Control` y la dirección base configurable. TT-CD-02 conserva a Front Door con WAF solo para la API si el Tech Lead lo autoriza por separado; este ADR no lo decide.
- Terraform (TT-AZ-02) define **dos cuentas de almacenamiento**: la pública con lectura anónima a nivel Blob y la privada con `allowBlobPublicAccess = false`. Las pruebas de seguridad negativa de la spec 013 (CA-03) agregan una petición anónima a una foto privada, que debe responder error de acceso.
- Los dominios permitidos de `fotoUrl` (spec 017, R-05) son la dirección del almacenamiento público y, si se adopta la opción E, la de Cloudflare.
- Si cambia alguna tarifa citada, se actualiza este ADR.
