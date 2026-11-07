# ADR-007 — CDN de las imágenes públicas

> **Estado:** propuesta, pendiente de aprobación del Tech Lead (consulta de documentación y tarifas: 2026-11-06). El Tech Lead indicó la opción D (Blob público sin CDN de pago); el ADR sigue como propuesta hasta que él lo apruebe al fusionar.
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
- Como referencia, Cloudflare publica un plan **Free de 0 USD al mes que incluye CDN**, pensado para proyectos personales o que no son críticos para el negocio y sin SLA de disponibilidad ([planes de Cloudflare](https://www.cloudflare.com/plans/network-cdn/)); y, por omisión, guarda en caché los archivos por extensión, entre ellas JPG, PNG, GIF, WEBP y SVG ([comportamiento de caché](https://developers.cloudflare.com/cache/concepts/default-cache-behavior/)).

## Opciones comparadas
| Opción | Se puede crear hoy | Costo | Cumple «CDN» | Riesgo para lo privado |
|---|---|---|---|---|
| A. Azure CDN Standard from Microsoft (classic) | **No** (creación de perfiles retirada) | — | — | — |
| B. Azure Front Door Standard | Sí | 35 USD al mes de base, más solicitudes y salida; sin nivel gratuito | Sí | Bajo si solo se asocia al origen público |
| C. Azure Front Door Premium | Sí | 330 USD al mes de base | Sí | Bajo |
| D. Blob público sin CDN, con `Cache-Control` largo | Sí | Almacenamiento más salida; 100 GB al mes gratis | No es CDN | Bajo si la cuenta pública es otra |
| E. Cloudflare Free delante del Blob público | Sí | 0 USD al mes (plan Free) | Sí, de otro proveedor | **No adoptada**; queda como alternativa futura |

## Decisión
1. **No se crea ningún perfil de CDN de pago.** Front Door Standard (opción B) es el único producto de Azure que se puede crear y cuesta 35 USD al mes aunque no tenga tráfico; la regla de costo cero del equipo lo descarta. Front Door Premium (opción C) queda descartada por costo y porque su diferencia (WAF y Private Link) no la necesita una imagen pública.
2. **Decisión del Tech Lead: opción D.** Las imágenes públicas se sirven desde un contenedor de Blob de **lectura anónima a nivel Blob** (sin listado) con encabezado `Cache-Control` largo e inmutable por nombre de archivo (el nombre lleva el hash del contenido). La dirección base de las imágenes sale de configuración (`Perfil:HostsFoto` y su equivalente en los otros servicios), de modo que **si algún día se pone una CDN delante no cambia código ni datos**, solo el valor de esa dirección y los dominios permitidos.
3. **Cloudflare (opción E) no se adopta.** Queda registrada solo como alternativa futura, sin recomendación: antes de considerarla habría que leer en su documentación las condiciones del plan (por ejemplo, si exige un dominio propio y sus límites de uso), que este ADR no verificó.
4. **Las fotos de verificación son siempre privadas y nunca pasan por una CDN**, cualquiera que sea la opción: viven en una **cuenta de almacenamiento distinta** a la de las imágenes públicas, con `allowBlobPublicAccess = false` (el ajuste de la cuenta anula cualquier contenedor público), se entregan solo con enlaces firmados de 5 minutos y cada acceso se registra. Esto refuerza el R-01 de la spec 013 (contenedores separados) con una separación de cuenta: sin ella, habilitar la lectura anónima para las imágenes públicas la habilitaría en la misma cuenta que guarda las fotos privadas.

## Cumplimiento del punto 15c de los lineamientos
**Con esta decisión el punto 15c («Imágenes en el CDN de la nube») no se cumple por completo.** Queda registrado de forma explícita para que nadie lo dé por cubierto.

| Aspecto | Estado con la opción D |
|---|---|
| Las imágenes se guardan y se sirven desde un servicio de almacenamiento de la nube (Azure Blob Storage) por dirección pública | Se cumple |
| Las respuestas llevan `Cache-Control` largo e inmutable, de modo que el navegador y la app reutilizan la imagen sin volver a pedirla | Se cumple |
| La dirección base es configurable, de modo que una CDN se puede poner delante sin cambiar código ni datos | Se cumple |
| Existe una **red de distribución de contenido** (CDN) con nodos de borde delante del almacenamiento | **No se cumple**: cada petición que no está en la caché del cliente llega al Blob, que está en una sola región |

- **Riesgo académico:** el docente evalúa con prompts sobre el repositorio y el punto 15c pide una CDN. Al no haber un recurso de CDN, la fila puede calificarse como incompleta o no cumplida. El riesgo es de calificación; no afecta el funcionamiento ni la seguridad.
- **Cómo se explicaría al profesor:** la CDN gestionada de Azure que aplica a este caso cuesta 35 USD al mes aunque no tenga tráfico y el proyecto opera con costo cero. Azure CDN classic ya no admite perfiles nuevos. Por ello el equipo documentó la decisión en este ADR, sirve las imágenes con caché de larga duración desde el almacenamiento de la nube y dejó la dirección base configurable para colocar una CDN delante sin tocar código. Es una decisión de costo tomada de forma consciente y documentada, no una omisión.
- `docs/CUMPLIMIENTO_LINEAMIENTOS.md` registra la fila 15c como parcial con esta explicación.

## Costo
- Opción D: almacenamiento a 0.0208 USD por GB al mes (unos 0.21 USD por cada 10 GB) y salida gratis hasta 100 GB al mes; después, 0.087 USD por GB. Con las fotos reducidas de tamaño (R-03 de la spec 013), el uso del piloto queda por debajo de esos 100 GB.
- Opción E (no adoptada): 0 USD por la CDN según el plan Free; el almacenamiento y el origen costarían igual que en D.
- Opción B, solo como referencia para el líder: la cuota base, cobrada por hora de uso, equivale a unos 12.8 USD para la ventana de evaluación del 25 de noviembre al 5 de diciembre (35 USD × 11 días ÷ 30), más solicitudes y salida. Saldría del crédito de Azure for Students (ADR-006). **No se recomienda ni se despliega.**

## Consecuencias
- TT-API-10 no crea ningún recurso de CDN; implementa el contenedor público, el encabezado `Cache-Control` y la dirección base configurable. TT-CD-02 conserva a Front Door con WAF solo para la API si el Tech Lead lo autoriza por separado; este ADR no lo decide.
- Terraform (TT-AZ-02) define **dos cuentas de almacenamiento**: la pública, con lectura anónima a nivel Blob, y la privada, con `allowBlobPublicAccess = false`. Hoy `deploy/modules/shared/main.tf` declara una sola (`azurerm_storage_account.files`, con `allow_nested_items_to_be_public = false` y los contenedores privados `imagenes` y `respaldos`); esa queda como la privada y hay que agregar la pública. Las pruebas de seguridad negativa de la spec 013 (CA-03) agregan una petición anónima a una foto privada, que debe responder error de acceso.
- Los dominios permitidos de `fotoUrl` (spec 017, R-05) son la dirección del almacenamiento público; si en el futuro se pone una CDN delante, se agrega su dominio.
- El punto 15c queda parcial (sección «Cumplimiento del punto 15c»).
- Si cambia alguna tarifa citada, se actualiza este ADR.
