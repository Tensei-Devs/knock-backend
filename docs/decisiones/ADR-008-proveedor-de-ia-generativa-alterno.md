# ADR-008 — Proveedor de IA generativa: Azure OpenAI con cuota y alterno de contingencia

> **Estado:** propuesta, pendiente de aprobación del Tech Lead (constancia y consulta de documentación: 2026-11-06).
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
| B. Gemini API, nivel gratuito (Google) | 0 USD; el nivel gratuito incluye modelos como Gemini 3.1 Flash-Lite y Gemini 2.5 Flash-Lite ([precios](https://aistudio.google.com/docs/pricing)); los límites por minuto y por día se consultan en AI Studio y se aplican por proyecto ([límites](https://aistudio.google.com/docs/rate-limits)) | En el nivel gratuito Google **usa el contenido enviado y las respuestas para mejorar sus productos**, y revisores humanos pueden leerlos; sus términos piden no enviar información sensible, confidencial ni personal a los servicios sin costo ([términos](https://aistudio.google.com/docs/terms)) | **Alterno de contingencia, con condiciones** |
| C. Modelos de pago de otro proveedor (por ejemplo DeepSeek, que ya usa la pre-revisión del CI con saldo prepagado) | Saldo prepagado | Sale hacia un tercero cuyo tratamiento de datos no se evaluó | Descartada: tiene costo y no se evaluó su tratamiento de datos |
| D. Solo plantillas locales | 0 USD | Nada sale | Descartada: la IA generativa es obligatoria; las plantillas son el respaldo, no el proveedor |

## Decisión
1. **Proveedor principal: Azure OpenAI**, porque ya hay cuota y es el proveedor previsto en la decisión técnica del proyecto. El modelo (`gpt-5-mini`, salvo que TT-IA-01 compruebe que no se puede desplegar) y el tope de gasto por persona y por día se fijan en TT-IA-01.
2. **No se contrata ni se activa nada con esta decisión.** El despliegue en Azure se crea solo desde Terraform en TT-IA-01 y consume del crédito (ADR-006).
3. **Alterno de contingencia sin costo: Gemini API en su nivel gratuito**, solo si Azure OpenAI no se puede desplegar. Condiciones obligatorias, porque Google puede usar y revisar el contenido del nivel gratuito:
   - Pasa primero por el **mismo filtro de datos personales** de TT-IA-01, sin excepción.
   - Se envía **solo texto** ya filtrado, con la mínima información que pide la skill `ai-features` (categoría, descripción y colonia). **No se envían fotos** ni nombres de personas. El filtro de TT-IA-01 enmascara teléfonos, CURP, direcciones y correos, pero no nombres, y el historial del chat es texto libre: por eso **el historial del chat no se envía** al alterno mientras el filtro no enmascare también nombres; el resumen del chat responde con la plantilla local.
   - Nunca se envían secretos, documentos de identidad ni datos de verificación.
   - La llave se guarda en Key Vault y no se escribe en el repositorio; el cliente se oculta tras la misma interfaz que el de Azure OpenAI, de modo que cambiar de proveedor es configuración.
   - Si se excede el límite del nivel gratuito, responde la plantilla local; no se pasa a un plan de pago sin la decisión del Tech Lead.
4. El proveedor alterno **no se implementa** mientras Azure OpenAI funcione. Este ADR solo deja fijadas sus condiciones.

## Costo
- Azure OpenAI: pago por token con cargo al crédito; el consumo se limita con el tope por persona y por día de TT-IA-01 y se revisa cada semana junto con el resto del gasto (ADR-006).
- Gemini API gratuito: 0 USD, con las condiciones de privacidad de la decisión 3.

## Consecuencias
- TT-IA-01 sigue siendo el único punto de acceso a la IA: el filtro de datos personales vive ahí y es previo a cualquier proveedor.
- El Tech Lead decide si aprueba este ADR y si autoriza el gasto en tokens de Azure OpenAI con el crédito.
- Si la cuota cambia o Microsoft publica tarifas utilizables, se actualiza este ADR.
