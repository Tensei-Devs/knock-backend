# ADR-008 — Proveedor de IA generativa: solo Azure OpenAI, con plantillas locales como respaldo

> **Estado:** aceptada el 2026-11-07 por el Tech Lead y corregida el mismo día: el proveedor de IA generativa es solo de Azure (Azure OpenAI) porque la entrega lo exige; Gemini y cualquier proveedor fuera de Azure quedan descartados. El gasto en tokens de Azure OpenAI no queda autorizado con este ADR: se decide en TT-IA-01.
> **Complementa:** las secciones 1 y 12 de `docs/DECISION_TECNICA.md`, la skill `ai-features` y la tarea TT-IA-01.
> **Tarea:** TT-GES-11 (KNOCK-183) · Impacto de implementación: TT-IA-01, HU-057, HU-058, HU-059.

## Contexto
La IA generativa es obligatoria para la entrega y el proveedor previsto es Azure OpenAI (`DECISION_TECNICA.md`, sección 1). La tarea pedía decidir un proveedor alterno **si el 11 de noviembre la cuota de Azure OpenAI seguía en cero**. Las plantillas locales solo responden cuando el proveedor falla. El filtro de datos personales de TT-IA-01 (enmascarar teléfonos, CURP, direcciones y correos, y rechazar imágenes de documentos) aplica a **cualquier** proveedor.

## Constancia de la cuota de Azure OpenAI
El 2026-11-06 se consultó, con permisos de solo lectura, la cuota de la suscripción Azure for Students en las regiones que permite la política (ADR-006):

```
az cognitiveservices usage list -l <región>
```

| Región | `gpt-4o-mini` Global Standard | `gpt-4.1-mini` Global Standard | `gpt-5-mini` Global Standard |
|---|---|---|---|
| West US | 2000 | 5000 | 1000 |
| France Central | 2000 | 5000 | 1000 |
| Norway East | 2000 | 5000 | 1000 |
| North Central US | 2000 | 5000 | 1000 |

La unidad es miles de tokens por minuto. En las cuatro regiones consultadas el uso actual es 0 y **el límite es mayor que cero** para esos modelos, y el límite de recursos de Azure OpenAI (S0) por región es 1 con 0 creados. La consulta no admite la ubicación Chile Central. Esta constancia prueba que la cuota **no está en cero**; no prueba que el despliegue de un modelo se pueda crear, porque crearlo es un cambio de infraestructura que corresponde a TT-IA-01. Si el 11 de noviembre el despliegue falla por falta de cuota, se repite la consulta y se activa el alterno de la decisión 3.

**Ciclo de vida de los modelos.** Tener cuota no basta: la API de Azure (`az cognitiveservices model list -l westus`, consulta del 2026-11-06) marca a `gpt-4o-mini` (2024-07-18) como `Deprecating`, es decir, ya no se ofrece a clientes nuevos de la suscripción y puede no poder crearse; a `gpt-4.1-mini` (2025-04-14) como `Legacy`, y a `gpt-5-mini` (2025-08-07) como `GenerallyAvailable`. Los dos primeros se retiran el **14 de abril de 2027** y `gpt-5-mini` el **13 de julio de 2027** ([ciclo de vida de los modelos](https://learn.microsoft.com/en-us/azure/foundry/openai/concepts/model-retirements)); las tres fechas quedan después de la ventana de evaluación (25 de noviembre al 5 de diciembre de 2026). Por eso el modelo preferido es `gpt-5-mini`.

## Opciones comparadas
| Opción | Costo | Datos enviados | Estado |
|---|---|---|---|
| A. Azure OpenAI (Global Standard, `gpt-5-mini`; `gpt-4.1-mini` como segunda opción) | Pago por token con cargo al crédito de Azure for Students; la página oficial de precios no muestra tarifas utilizables (aparecen como «$-»), por eso la tarifa se fija con la calculadora de Azure en TT-IA-01 | Los define Azure; TT-IA-01 confirma retención y entrenamiento | **Elegida como proveedor principal** |
| B. Gemini API, nivel gratuito (Google) | 0 USD; el nivel gratuito incluye modelos como Gemini 3.1 Flash-Lite y Gemini 2.5 Flash-Lite ([precios](https://aistudio.google.com/docs/pricing)); los límites por minuto y por día se consultan en AI Studio y se aplican por proyecto ([límites](https://aistudio.google.com/docs/rate-limits)) | En el nivel gratuito Google **usa el contenido enviado y las respuestas para mejorar sus productos**, y revisores humanos pueden leerlos; sus términos piden no enviar información sensible, confidencial ni personal a los servicios sin costo ([términos](https://aistudio.google.com/docs/terms)) | **Descartada:** no es de Azure; la entrega exige IA de Azure y otro proveedor implicaría un proyecto adicional |
| C. Modelos de pago de otro proveedor (por ejemplo DeepSeek, que ya usa la pre-revisión del CI con saldo prepagado) | Saldo prepagado | Sale hacia un tercero cuyo tratamiento de datos no se evaluó | Descartada: tiene costo y no se evaluó su tratamiento de datos |
| D. Solo plantillas locales | 0 USD | Nada sale | Descartada: la IA generativa es obligatoria; las plantillas son el respaldo, no el proveedor |

## Decisión
1. **Proveedor principal: Azure OpenAI**, porque ya hay cuota y es el proveedor previsto en la decisión técnica del proyecto. El modelo (`gpt-5-mini`, salvo que TT-IA-01 compruebe que no se puede desplegar) y el tope de gasto por persona y por día se fijan en TT-IA-01.
2. **No se contrata ni se activa nada con esta decisión.** El despliegue en Azure se crea solo desde Terraform en TT-IA-01 y consume del crédito (ADR-006).
3. **Respaldo sin costo: plantillas locales.** Si Azure OpenAI no se puede desplegar, se acaba el crédito o se alcanza el tope de gasto, la app responde con las plantillas locales y no envía nada a ningún tercero. No se usa ningún proveedor fuera de Azure.
4. No se integra Gemini ni ningún otro proveedor fuera de Azure. El filtro de datos personales de TT-IA-01 sigue siendo previo a cualquier llamada a Azure OpenAI.

## Condiciones de privacidad del nivel gratuito de Gemini (evaluadas y descartadas)
Consulta del 2026-11-06 en la documentación oficial de Google. Solo se anota lo que se leyó en la fuente; lo que no se pudo confirmar está al final de la sección.

Fuente principal: términos adicionales de la Gemini API, [aistudio.google.com/docs/terms](https://aistudio.google.com/docs/terms) (la dirección `ai.google.dev/gemini-api/terms` redirige ahí; la página indica «Effective March 23, 2026»).

**Qué es el servicio no pagado.** Los términos llaman «Unpaid Services» a cualquier servicio ofrecido sin costo, entre ellos la interacción directa con Google AI Studio y la cuota gratuita de la Gemini API.

**Uso de los datos en el servicio no pagado:**
- Google usa el contenido enviado y las respuestas generadas para proveer, mejorar y desarrollar sus productos, servicios y tecnologías de aprendizaje automático, conforme a su Política de Privacidad.
- Revisores humanos pueden leer, anotar y procesar la entrada y la salida de la API. Antes de que las vean, Google desvincula esos datos de la cuenta de Google, la clave de API y el proyecto de Cloud.
- La advertencia textual de los términos es: «Do not submit sensitive, confidential, or personal information to the Unpaid Services.»
- La tabla de precios ([aistudio.google.com/docs/pricing](https://aistudio.google.com/docs/pricing)) lista la fila «Used to improve our products» con «Yes» en el nivel gratuito y «No» en el de pago para los modelos del nivel estándar que se leyeron.

**Servicio de pago (para comparar):** Google no usa las entradas ni las respuestas para mejorar sus productos; las registra por un tiempo limitado solo para detectar y prevenir violaciones de su política de uso prohibido, mantener la seguridad y cumplir obligaciones legales. El acceso a la Gemini API cuenta como servicio de pago solo cuando se hace desde un proyecto de Cloud asociado a una cuenta de facturación activa ([términos](https://aistudio.google.com/docs/terms); [facturación](https://aistudio.google.com/docs/billing)). Pasar a ese nivel exige vincular una cuenta de facturación y es un gasto, por lo que **contradice el costo cero**.

**Restricciones:**
- **Edad:** hay que tener 18 años o más para usar las APIs, y los términos prohíben usarlas en un sitio, aplicación o servicio dirigido a menores de 18 años o que probablemente usen menores.
- **Región:** los servicios solo pueden usarse en una región disponible. Para clientes de API puestos a disposición de personas usuarias en el Espacio Económico Europeo, Suiza o el Reino Unido solo pueden usarse servicios de pago. Para quien está en esas regiones, los términos de datos del servicio de pago aplican también al gratuito.
- **Límites de uso:** los límites se aplican por proyecto y no por llave; Google indica que no están garantizados y que la capacidad real puede variar ([límites](https://aistudio.google.com/docs/rate-limits)). Las cifras exactas se consultan en AI Studio.
- **Otros documentos:** los términos generales de IA generativa de Google (`policies.google.com/terms/generative-ai`) ya no aplican desde el 22 de mayo de 2024 salvo contratos firmados que los citen, y no mencionan la Gemini API ni a revisores humanos; no sirven como fuente para esta decisión.

**No se pudo confirmar:**
- El **periodo de retención** de los datos del servicio no pagado: los términos no lo indican. La página de registros ([aistudio.google.com/docs/logs-policy](https://aistudio.google.com/docs/logs-policy)) fija 55 días como máximo por omisión, pero solo para proyectos con facturación habilitada y no trata el nivel gratuito.
- Si un dato enviado puede **borrarse a solicitud** en el nivel gratuito.
- Si **México** figura entre las regiones disponibles y si las personas usuarias de Knock se consideran dentro de las restricciones de región; no se encontró una lista de regiones en las páginas leídas.
- La **Política de Privacidad de Google** a la que remiten los términos no se leyó en esta consulta.
- Si Knock puede ser usada por menores de 18 años: el repositorio solo anota validar la mayoría de edad en el registro de técnicos (`docs/referencia/FUNCIONES_Y_FLUJOS.md`) y no define una edad mínima para quien solicita un servicio.
- La disponibilidad de cada modelo gratuito en la ventana de evaluación (25 de noviembre al 5 de diciembre de 2026): se vuelve a revisar al implementar.

## Por qué se descarta Gemini
La entrega exige que la IA generativa sea de Azure; usar otro proveedor implicaría un proyecto adicional solo para la IA. Además, en el nivel gratuito Google puede conservar y revisar lo que se le envíe (sección anterior). Las plantillas locales son el respaldo, no un proveedor de IA generativa.

## Costo
- Azure OpenAI: pago por token con cargo al crédito; el consumo se limita con el tope por persona y por día de TT-IA-01 y se revisa cada semana junto con el resto del gasto (ADR-006).
- Plantillas locales: 0 USD.

## Consecuencias
- TT-IA-01 sigue siendo el único punto de acceso a la IA: el filtro de datos personales vive ahí y es previo a cualquier proveedor.
- El Tech Lead aprobó este ADR y lo corrigió para que el proveedor sea solo Azure OpenAI. Gemini queda descartado. El proveedor principal no cambia.
- Sigue pendiente la decisión sobre el gasto en tokens de Azure OpenAI con el crédito; se toma en TT-IA-01.
- Si la cuota cambia o Microsoft publica tarifas utilizables, se actualiza este ADR.
