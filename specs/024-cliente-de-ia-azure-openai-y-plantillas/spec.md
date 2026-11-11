# Spec 024 — Cliente de IA con Azure OpenAI y plantillas locales de respaldo

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo (verificadas en Jira el 2026-11-09):** KNOCK-63 «TT-IA-01 · Cliente de Azure OpenAI con filtro de datos personales, límites de costo y respaldo manual»
> **Responsable sugerido:** José Julián (decisión del tope y ADR en el Sprint 4; implementación en el Sprint 5) · **Ola:** C · **Prioridad:** P2
> **Servicio:** Mercado · **Módulo:** Assistant · **Colecciones:** `uso_ia` (nueva)
> **Implementación:** Sprint 5; esta spec deja el contrato y las reglas cerradas para que las funciones de IA de HU-057, HU-058 y HU-059 lo usen sin duplicar el acceso.

## 1. Objetivo
Tener un solo punto de acceso a la IA generativa en el backend: un cliente de Azure OpenAI que censura los datos personales antes de enviar, respeta topes de consumo por persona y por día, se apaga solo cuando el presupuesto se agota y, si el servicio falla, responde con plantillas locales que no salen a ningún tercero.

## 2. Historia y alcance
**2.1 Historia**
- **TT-IA-01** — Como equipo, necesitamos un solo cliente de Azure OpenAI con filtro de datos personales, límites de costo y respaldo, para que todas las funciones de IA lo usen igual y ninguna filtre datos por su cuenta.

**2.2 Dentro**
- La interfaz interna `IAiClient` del módulo `Assistant`, único punto que conoce Azure OpenAI.
- El filtro de datos personales previo al envío (teléfonos, CURP, RFC, correos, direcciones, tarjetas) y el rechazo de imágenes que no sean fotos de un problema (las fotos de una solicitud son privadas y entran por `archivoId`).
- La salida estructurada con esquema JSON y su validación; si no cumple, se descarta y se usa la plantilla.
- Los topes de consumo por persona y por día y el tope global diario, con apagado automático y respaldo local.
- El registro de uso sin contenido: función, modelo, versión del prompt, tokens, latencia y origen.
- El respaldo con plantillas locales versionadas, con el mismo esquema de salida.
- Las pruebas con un servicio simulado; ninguna prueba llama al servicio real.

**2.3 Fuera**
- Las funciones de producto que usan la IA: asistente Knock (HU-057), resumen del chat (HU-058) y borrador de cotización (HU-059).
- Cualquier endpoint público: esta spec no expone rutas y el gateway no enruta nada al módulo.
- El despliegue del recurso de Azure OpenAI y la fijación del gasto en tokens: son de la infraestructura (Terraform) y del ADR-008.
- El modelo de aprendizaje automático de «Recomendadas» y del rango de precio (ML.NET): es otra spec.
- La moderación del contenido que sube una persona: es de la spec 013 y ocurre antes de que el texto llegue aquí.
- La traducción o el resumen de conversaciones completas, el historial del chat y el envío de mensajes.

## 3. Requisitos
- **R-01:** Existe una sola interfaz `IAiClient` en `Modules/Assistant/Services/` con la operación `GenerarAsync<T>(SolicitudIa, CancellationToken)`. Ningún otro módulo ni servicio referencia el paquete de Azure OpenAI ni llama al proveedor por su cuenta; una prueba de arquitectura lo verifica.
- **R-02:** Toda solicitud pasa primero por `FiltroDatosPersonales`, que devuelve el texto censurado y la lista de tipos encontrados (solo los tipos y cuántos, nunca los valores). El cliente envía únicamente el texto censurado; el texto original no se guarda ni se registra. El `UsuarioId` de la solicitud solo se usa para los topes y el registro de uso: nunca se envía al proveedor, igual que nombres, teléfonos e identificadores de la cuenta.
- **R-03:** El filtro enmascara, como mínimo: teléfonos de 10 dígitos con o sin lada y separadores, CURP, RFC, correos electrónicos, números de tarjeta de 16 dígitos y direcciones con la forma «calle + número» o «calle + número + colonia». Una cadena que parezca un documento de identidad se censura entera aunque no cumpla el patrón completo (*fallar cerrado*).
- **R-04:** Si la solicitud incluye imágenes, se aceptan **identificadores de archivo** (`archivoId`) del módulo de archivos privados (TT-API-05) con `uso = solicitud`; la solicitud admite de 1 a 5 (spec 020) y el asistente usa solo las tres primeras, sin rechazar la solicitud por tener más (*decisión de revisión, confirmar*), estado `listo` y del mismo `UsuarioId`. El cliente obtiene la versión reducida (sin metadatos) por `IMediaApi` y la envía al proveedor dentro de la petición; ningún enlace firmado sale del servidor. Se rechaza cualquier otra entrada: direcciones (`https`, `data:` u otras), archivos de otro `uso` (perfil, trabajo), archivos de otra persona y cualquier imagen de verificación de identidad, que vive en otro servicio y otra cuenta de almacenamiento (spec 025) y no es accesible desde aquí. Con una imagen rechazada no se llama al proveedor y se responde con la plantilla.
- **R-05:** El proveedor devuelve salida estructurada con el esquema JSON de la función. La respuesta se deserializa a un tipo cerrado (sin miembros desconocidos) y se valida con FluentValidation; si no cumple el esquema, falta un campo o sobra, se descarta y se usa la plantilla. Nunca se devuelve texto libre al llamador.
- **R-06:** Los textos de entrada y de salida tienen tope: la entrada censurada no supera 4 000 caracteres y cada campo de texto de la salida, 500; un valor mayor se recorta antes de validar y se anota como `recortado`.
- **R-07:** Los topes de consumo salen de configuración: `Asistente:Limites:TokensPorUsuarioPorDia` (por omisión 20 000) y `Asistente:Limites:TokensGlobalesPorDia` (por omisión 200 000). Antes de llamar se reserva el consumo máximo posible en Redis con una operación atómica; si la reserva no cabe en el tope de la persona o en el global, no se llama al proveedor y se responde con la plantilla y `motivo: budget_exhausted`.
- **R-08:** Al terminar la llamada se ajusta la reserva con los tokens reales que informa el proveedor. Los contadores viven en Redis con clave por día (`ia:consumo:{yyyyMMdd}` global y `ia:usuario:{sub}:{yyyyMMdd}`) y vencimiento de 48 h; si Redis no responde, el cliente no llama al proveedor y responde con la plantilla y `motivo: limit_unavailable` (no se gasta sin poder contar).
- **R-09:** El apagado es explícito y configurable: `Asistente:Habilitado` en falso responde siempre con la plantilla y `motivo: disabled`, sin tocar la red. Su valor por omisión es **falso**: el gasto solo se enciende cuando una persona aprueba el tope de KNOCK-63 y lo cambia en la configuración del entorno. Al agotarse el tope global del día, todas las llamadas responden con la plantilla hasta el día siguiente.
- **R-10:** El cliente tiene un tiempo máximo de 10 s por llamada (el de la ficha de TT-IA-01, no los 20 s de la guía de funciones de IA), un reintento solo ante 429, 5xx o error de red (nunca ante 400 o 401) y un cortacircuito que abre 60 s tras cinco fallos seguidos en la misma réplica. Agotado el tiempo o abierto el cortacircuito, se responde con la plantilla y `motivo: unavailable`.
- **R-11:** Las plantillas locales viven en `Modules/Assistant/Plantillas/<funcion>.md`, versionadas en el repositorio, y producen el mismo esquema de salida que el proveedor, sin datos personales y con el texto en español de México. El resultado indica `origen: plantilla` y el motivo.
- **R-12:** El registro de uso se guarda en `uso_ia` con `{ usuarioId, funcion, modelo, versionPrompt, tokensEntrada, tokensSalida, latenciaMs, origen, motivoRespaldo?, recortado, creadoEn }`; no guarda el texto, las imágenes, la salida ni ningún dato personal. El `usuarioId` es el `sub` del token.
- **R-13:** La clave del proveedor no vive en el repositorio: en Azure se usa identidad administrada; si se usa clave, sale de Key Vault y solo por configuración. La clave, el punto de conexión, el texto enviado y la salida nunca aparecen en los registros; el registro de la petición lleva `X-Correlation-Id`.
- **R-14:** Los prompts viven en `Modules/Assistant/Prompts/<funcion>.v<N>.md` con su número de versión; cambiar un prompt sube la versión y el registro de uso guarda cuál se usó, para poder comparar calidad y costo.
- **R-15:** Sin endpoint público no hay contrato OpenAPI nuevo; el módulo se registra con `AddAssistantModule()` y no se agrega al enrutamiento del gateway. Si una función futura necesita una ruta, se especifica en su propia historia.
- **R-16:** La salida de la IA nunca cambia estados, no envía cotizaciones, no aprueba verificaciones y no sanciona: es una sugerencia que una persona revisa y edita (constitución §2 y guía de funciones de IA).

## 4. Criterios de aceptación
- **CA-01:** Dado un texto con un teléfono, una CURP, un correo y una dirección, cuando se filtra, entonces la solicitud que sale hacia el proveedor no contiene ninguno de los cuatro valores y el resultado informa los tipos y cuántos se censuraron.
- **CA-02:** Dado un texto con un número de 18 caracteres que parece CURP pero no cumple el patrón completo, cuando se filtra, entonces se censura igual (fallar cerrado).
- **CA-03:** Dada una solicitud con una dirección de imagen (`https` o `data:`), con un `archivoId` de otra persona, de `uso = perfil` o no `listo`, cuando se procesa, entonces no se llama al proveedor, la respuesta viene de la plantilla y el motivo es `image_rejected`.
- **CA-04:** Dada una respuesta del proveedor que no cumple el esquema (campo faltante, campo extra o tipo distinto), cuando se valida, entonces se descarta y se usa la plantilla, sin error 500.
- **CA-05:** Dado el tope por persona agotado, cuando se pide una generación, entonces no se llama al proveedor y se responde con la plantilla y `motivo: budget_exhausted`.
- **CA-06:** Dado el tope global agotado, cuando pide cualquier persona, entonces todas reciben plantilla hasta el día siguiente.
- **CA-07:** Dadas dos solicitudes simultáneas que juntas superan el tope, cuando se ejecutan, entonces solo pasa la que cabe y la otra recibe plantilla (la reserva es atómica).
- **CA-08:** Dado `Asistente:Habilitado` en falso, cuando se pide una generación, entonces responde la plantilla con `motivo: disabled` y no hay tráfico de red.
- **CA-09:** Dado el proveedor caído, lento o con 429, cuando se agota el tiempo o los reintentos, entonces responde la plantilla con `motivo: unavailable` y el llamador recibe el mismo esquema que con éxito.
- **CA-10:** Dado un fallo de autenticación con el proveedor (401), cuando ocurre, entonces no se reintenta, se responde con plantilla y se registra el fallo sin la clave.
- **CA-11:** Dada una generación correcta, cuando termina, entonces hay un documento en `uso_ia` con tokens, latencia, modelo, versión del prompt y origen, y no contiene el texto ni la salida.
- **CA-12:** Dada una llamada desde cualquier módulo fuera de `Assistant`, cuando se inspecciona el código, entonces no existe una referencia al paquete de Azure OpenAI ni una llamada directa al proveedor.
- **CA-13:** Dado el registro de una llamada fallida por datos personales o por imagen rechazada, cuando se inspecciona, entonces no contiene el valor censurado ni la dirección de la imagen.
- **CA-14:** Dado Redis caído, cuando se pide una generación, entonces no se llama al proveedor y se responde con la plantilla y `motivo: limit_unavailable`.
- **CA-15:** Dada la configuración por omisión (sin `Asistente:Habilitado` en el entorno), cuando arranca el servicio y se pide una generación, entonces responde la plantilla con `motivo: disabled` y no hay tráfico de red.
- **CA-16:** Dada una generación correcta, cuando se inspecciona la petición enviada al proveedor simulado, entonces no contiene el `UsuarioId`, el `sub` ni ningún enlace firmado.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec024FiltroTests.Censura_Datos_Personales_CA_024_01` |
| CA-02 | `Spec024FiltroTests.Falla_Cerrado_CA_024_02` |
| CA-03 | `Spec024FotosTests.Rechaza_Imagen_No_Permitida_CA_024_03` |
| CA-04 | `Spec024EsquemaTests.Rechaza_Salida_Fuera_De_Esquema_CA_024_04` |
| CA-05 | `Spec024LimitesTests.Tope_Por_Persona_CA_024_05` |
| CA-06 | `Spec024LimitesTests.Tope_Global_CA_024_06` |
| CA-07 | `Spec024LimitesTests.Reserva_Atomica_CA_024_07` |
| CA-08 | `Spec024RespaldoTests.Apagado_Explicito_CA_024_08` |
| CA-09 | `Spec024RespaldoTests.Proveedor_No_Disponible_CA_024_09` |
| CA-10 | `Spec024RespaldoTests.Sin_Reintento_En_401_CA_024_10` |
| CA-11 | `Spec024UsoTests.Registra_Uso_Sin_Contenido_CA_024_11` |
| CA-12 | `Spec024ArquitecturaTests.Un_Solo_Punto_De_Acceso_CA_024_12` |
| CA-13 | `Spec024RegistrosTests.Sin_Valores_Censurados_CA_024_13` |
| CA-14 | `Spec024LimitesTests.Redis_Caido_No_Gasta_CA_024_14` |
| CA-15 | `Spec024RespaldoTests.Apagado_Por_Omision_CA_024_15` |
| CA-16 | `Spec024FiltroTests.Sin_Identificadores_En_La_Peticion_CA_024_16` |

## 5. Contrato
**No hay rutas públicas.** El contrato es la interfaz interna:

| Operación | Entrada | Salida |
|---|---|---|
| `IAiClient.GenerarAsync<T>` | `SolicitudIa { Funcion, UsuarioId, Texto, ArchivoIds[], VersionEsquema }` | `ResultadoIa<T> { Valor?, Origen, MotivoRespaldo?, TiposCensurados[], Recortado, LatenciaMs }` |

- `Funcion`: `asistente_knock`, `resumen_chat` o `borrador_cotizacion`; cada una tiene su esquema, su prompt y su plantilla.
- `Origen`: `proveedor` o `plantilla`. `MotivoRespaldo`: `budget_exhausted`, `disabled`, `unavailable`, `image_rejected`, `limit_unavailable`, `invalid_schema`, `no_fallback`.
- `Valor` es el tipo cerrado de la función, nunca texto libre ni `json` sin validar.

**Configuración:** `Asistente:Habilitado`, `Asistente:AzureOpenAI:Endpoint`, `Asistente:AzureOpenAI:ModeloDespliegue`, `Asistente:Limites:TokensPorUsuarioPorDia`, `Asistente:Limites:TokensGlobalesPorDia`, `Asistente:Limites:TiempoMaximoSegundos` (10). `Asistente:Habilitado` es falso por omisión. La llave no es configuración del repositorio: identidad administrada o Key Vault.

**Límites:** 4 000 caracteres de entrada censurada, 500 por campo de salida, 3 imágenes, 10 s por llamada, un reintento, 60 s de cortacircuito y los topes de tokens de R-07.

## 6. Modelo de datos
**`uso_ia`** (nueva, base `market`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `usuarioId` | string | `sub` del token |
| `funcion` | string | `asistente_knock`, `resumen_chat`, `borrador_cotizacion` |
| `modelo` | string | despliegue usado; `plantilla` cuando no hubo proveedor |
| `versionPrompt` | string | `asistente_knock.v3` |
| `tokensEntrada`, `tokensSalida` | int | 0 cuando no hubo proveedor |
| `latenciaMs` | int | tiempo total, incluido el respaldo |
| `origen` | string | `proveedor` o `plantilla` |
| `motivoRespaldo` | string? | uno de los de §5 |
| `recortado` | bool | verdadero si se recortó la entrada o una salida |
| `creadoEn` | date | UTC |

Índices: `{ usuarioId: 1, creadoEn: -1 }`, `{ creadoEn: -1 }` para el reporte de gasto y `{ funcion: 1, origen: 1, creadoEn: -1 }`. Sin TTL: el agregado de gasto se calcula sobre los documentos; una política de retención se decide con el reporte de costos.

**Sin contenido:** no se guardan el texto, las imágenes, la salida, la clave ni el punto de conexión.

El script `db/` de esta spec crea la colección y sus índices de forma idempotente.

## 7. Arquitectura y componentes
- Módulo nuevo `src/Knock.Market.Api/Modules/Assistant/` con `Services`, `Dtos`, `Validators`, `Data`, `Prompts` y `Plantillas`; expone solo `AddAssistantModule()`.
- `AiClient` implementa `IAiClient`: filtra, reserva, llama, valida, ajusta la reserva, registra y, si algo falla, delega en `PlantillaIa`.
- `FiltroDatosPersonales` es una clase pura sin dependencias, con patrones compilados y una lista de tipos; es el punto que mutan las pruebas de mutación.
- `ConsumoIaStore` es el único punto con Redis (`ia:consumo:*`, `ia:usuario:*`) y `UsoIaStore` el único con MongoDB (`uso_ia`); ninguno se mezcla con la lógica de negocio.
- `ProveedorIa` envuelve el cliente de Azure OpenAI y expone solo «pide esto y devuelve el JSON»; el resto del módulo no conoce el SDK.
- `PlantillaIa` lee las plantillas del disco al arrancar, las valida contra el esquema y las compone con los mismos campos censurados.
- `TimeProvider` para tiempos y `IClock` de negocio para el día de los topes (zona `America/Mexico_City`).
- Sin estado en el proceso salvo el cortacircuito, que es una optimización por réplica y no una regla de negocio.

## 8. Dependencias
- Spec 002 (estructura), 003 (MongoDB, Redis), 009 (JWT y límites), 010 (caché), 011 (registro y correlación), 013 y TT-API-05 (archivos privados por `IMediaApi` y moderación).
- ADR-008: Azure OpenAI es el único proveedor y las plantillas locales son el respaldo; cualquier proveedor fuera de Azure queda descartado. El ADR debe actualizarse (tarea N-5 del plan del sprint, sin clave en Jira todavía).
- Dependencia nueva: `Azure.AI.OpenAI` (paquete oficial de Azure, licencia MIT), con la versión estable fijada al implementar y registrada en `Memoria.md`; alternativa evaluada: llamar por REST con `System.ClientModel`. `Microsoft.Extensions.AI` no se adopta en esta spec para no agregar otra capa sin necesidad.
- Terraform (spec 005) crea el recurso y el despliegue del modelo; sin eso, el módulo arranca en modo plantilla.
- Las funciones de HU-057, HU-058 y HU-059 consumen `IAiClient`; esta spec no las implementa.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Enviar datos personales al modelo | Filtro previo que falla cerrado y prueba con teléfono, CURP, correo y dirección | CA-01, CA-02 |
| Enviar fotos de identificación o ajenas | Solo `archivoId` de `uso = solicitud`, `listo` y del mismo usuario; las de verificación viven en otro servicio y no son accesibles | CA-03 |
| Gastar tokens sin decisión | `Asistente:Habilitado` falso por omisión | CA-15 |
| Gasto descontrolado en tokens | Reserva atómica en Redis, tope por persona y global, apagado explícito | CA-05, CA-06, CA-07 |
| Gasto sin control cuando Redis falla | Sin contador no se llama al proveedor | CA-14 |
| Depender del proveedor para responder | Plantillas locales con el mismo esquema | CA-04, CA-09 |
| Fuga de la clave o del punto de conexión | Identidad administrada o Key Vault; nunca en el repositorio ni en los registros | CA-10, CA-13 |
| Respuesta del modelo con contenido dañino o formato inesperado | Esquema cerrado y validación; la persona revisa y edita | CA-04 |
| Uso de la IA para decidir por una persona | La salida es una sugerencia; ninguna ruta cambia estados | CA-12 |
| Reintentos que multiplican el gasto | Un solo reintento, solo ante 429/5xx/red, y cortacircuito | CA-09, CA-10 |
| Registros con el contenido enviado | `uso_ia` sin texto ni salida; filtro de datos personales en los registros | CA-11, CA-13 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Filtro, validación del esquema, topes, motivos de respaldo y armado de plantillas |
| Integración | Sí | Redis y MongoDB reales con `Testcontainers`: reserva atómica, ajuste, registro de uso y contadores por día |
| Seguridad negativa | Sí | Imágenes de otro origen, intentos de saltar el filtro y ausencia de datos personales en registros |
| Contrato | No | No hay ruta pública nueva; el contrato interno se prueba con las unitarias |
| Arquitectura | Sí | Un solo punto de acceso al SDK y ningún otro módulo lo referencia |
| Mutación | Sí | `FiltroDatosPersonales` y la condición de la reserva |
| Rendimiento | Sí | Una prueba acotada del filtro: 4 KB de texto censurados en menos de 5 ms, sin cobertura, como las pruebas de tiempo de la spec 011 |
| Aceptación | Sí | CA-01 a CA-16 con la tabla 4.1 |
| Base de datos | Sí | `DatabaseTest`: `uso_ia` y sus índices creados de forma idempotente |
| Humo y extremo a extremo | Sí | En el entorno de evaluación: con `Asistente:Habilitado = false`, una solicitud con foto se crea y no se llama al proveedor; con el flag activo y datos sintéticos, una solicitud de prueba recibe sugerencia o plantilla de respaldo (flujo de la spec 020) |
| Regresión | Sí | Las suites de Mercado y de las specs 015 a 023 siguen en verde; el módulo nuevo no cambia rutas existentes y el contrato OpenAPI no cambia |
| CI/CD | Sí | Job `gate` de `ci-pr.yml` en verde; `openapi-diff` comprueba que el contrato de Mercado no cambió; el despliegue arranca con `Asistente:Habilitado = false` y `/health/ready` en verde |
| Análisis estático | Sí | Semgrep (job `sast`) sin hallazgos nuevos, incluido `p/secrets` para la clave del proveedor; complejidad máxima 10 |
| Vulnerabilidades y licencias | Sí | NuGetAudit, `dotnet list package --vulnerable`, OSV-Scanner y dependency-review para `Azure.AI.OpenAI` (MIT) sin altas ni críticas; versión registrada en `Memoria.md` |
| DAST | No | No hay ruta pública nueva |

**Servicio simulado:** todas las pruebas usan un `HttpMessageHandler` falso que devuelve respuestas preparadas (éxito, esquema inválido, 429, 401, tiempo agotado). Ninguna prueba gasta tokens ni envía datos a Azure. La prueba contra el servicio real se corre a mano, una vez, y no entra al CI.

**Gates:** los de `AGENTS.md`; la prueba de arquitectura comprueba que el conjunto evaluado no está vacío y que solo `Assistant` referencia `Azure.AI.OpenAI`.

## 11. Escenarios
**Positivos**
- Una función pide una descripción sugerida y recibe la del proveedor, con el teléfono del texto censurado.
- El proveedor responde 429 dos veces; el cliente reintenta una, se agota el tiempo y responde la plantilla.
- Se agota el tope global del día; todas las funciones responden con plantilla hasta mañana.
- Se cambia el prompt a `v4` y el registro de uso lo refleja.

**Negativos**
- El texto trae una CURP y una dirección: no salen del servidor.
- La función intenta mandar la foto de la INE: se rechaza antes de la red.
- Alguien intenta llamar al proveedor desde otro módulo: la prueba de arquitectura falla.
- La respuesta trae un campo extra: se descarta y se usa la plantilla.

**Errores esperados**
- Nunca un 500 por fallo del proveedor: siempre plantilla con el esquema.
- 401 del proveedor: sin reintento, plantilla y registro del fallo.
- Redis caído: plantilla, sin gasto.

**Casos límite**
- Texto de 4 000 y de 4 001 caracteres.
- Teléfono con `+52`, con espacios, con guiones y pegado a texto.
- CURP en minúsculas.
- Tres imágenes permitidas y una cuarta rechazada.
- Reserva exacta en el tope (cabe) y un token por encima (no cabe).
- Cambio de día durante una llamada: la reserva y el ajuste usan la misma clave.

## 12. Definición de terminado
- Los 16 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales (`python scripts/sdd_lint.py --trace 024` sin faltantes).
- Job `gate` en verde; el análisis de dependencias acepta `Azure.AI.OpenAI` sin hallazgos altos ni críticos.
- La prueba del filtro usa textos con teléfono, CURP, RFC, correo, tarjeta y dirección, y comprueba que ninguno llega al cuerpo de la petición simulada.
- La prueba de arquitectura demuestra que el módulo es el único que referencia el SDK.
- `db/` crea `uso_ia` e índices de forma idempotente; `Memoria.md` registra la dependencia, su licencia y su versión.
- `Asistente:Habilitado` en falso arranca el servicio sin tocar la red, y así se documenta para el despliegue sin cuota.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Un solo punto de acceso:** si cada función llamara al proveedor por su cuenta, cada una tendría su propio filtro y su propio tope; el cliente central hace que la regla de datos personales sea imposible de olvidar.
- **Plantillas locales como respaldo, no como proveedor:** el ADR-008 lo fija; además es lo único que responde sin costo cuando el crédito se agota.
- **Reserva antes de llamar, ajuste después:** el gasto se cuenta antes de gastarse; contar después permitiría pasarse del tope con llamadas simultáneas.
- **Sin contador no hay llamada:** un tope que se salta cuando Redis falla no es un tope.
- **10 s de tiempo máximo:** la ficha de TT-IA-01 lo pide y la persona está esperando en la pantalla; la guía de funciones de IA decía 20 s y se ajusta a la ficha.
- **El registro de uso no guarda contenido:** sirve para costos y calidad, no para reconstruir conversaciones.
- **Sin endpoint público:** la IA de esta etapa alimenta pantallas existentes; una ruta propia sería una superficie de abuso nueva sin una historia que la pida.
- **El motivo del respaldo es parte de la respuesta:** así la pantalla puede decir «Sugerido por IA» o «Sugerencia automática» sin adivinar.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **La reserva de tokens es en Redis y el registro es en MongoDB.** El contador debe ser atómico y rápido; el registro es para el reporte. Alternativa: contar en MongoDB con `$inc`, que agrega una escritura por llamada y no da el mismo aislamiento con varias réplicas.
2. **Si Redis no responde, no se llama al proveedor.** Es más estricto que el resto de la aplicación, donde la caché falla en silencio; aquí lo que se pierde no es velocidad sino control de gasto. Alternativa: permitir un número pequeño de llamadas sin contador, que es exactamente el agujero que se quiere cerrar.
3. **El cortacircuito es por réplica y en memoria.** Con dos réplicas, como máximo se hacen cinco llamadas fallidas por réplica antes de abrir; un cortacircuito compartido en Redis agrega una dependencia al camino crítico.
4. **Un solo reintento.** La ficha pide reintentos; con 10 s de presupuesto y una persona esperando, dos reintentos ya no caben.
5. **Se rechaza la imagen en vez de ignorarla.** Mandar el texto sin la imagen daría una sugerencia peor y sin avisar; el rechazo explícito deja el motivo visible. Alternativa: ignorar la imagen y seguir, que oculta el error a la función que llama.
6. **El filtro se aplica también a la salida antes de guardarla en el registro.** Aunque la salida no se guarda, el llamador podría registrarla; la plantilla y el cliente no devuelven valores con forma de dato personal.
7. **La lista de tipos censurados se devuelve al llamador.** Sirve para que la función avise «quitamos datos personales del texto»; no expone los valores, solo los tipos y cuántos.
8. **La versión del prompt va en el nombre del archivo.** Evita una tabla de versiones y hace evidente en el `diff` qué cambió.
9. **Se usa `Azure.AI.OpenAI` y no `Microsoft.Extensions.AI`.** Una capa menos y un paquete menos que auditar; si otra función necesita abstracción, se evalúa entonces.

**Agregadas en la revisión del 9 de noviembre**

10. **Las imágenes entran por `archivoId` privado, no por dirección pública.** La spec 020 dejó las fotos de las solicitudes en el contenedor privado (TT-API-05); un prefijo público ya no existe. Se envía la versión reducida dentro de la petición para que ningún enlace firmado salga del servidor. Alternativa: mandar un enlace de lectura de 5 minutos al proveedor, que deja una dirección válida fuera de Knock.
11. **`Asistente:Habilitado` es falso por omisión.** Costo cero: nada gasta tokens hasta que una persona apruebe el tope (KNOCK-63) y lo encienda por entorno. Alternativa: encendido por omisión con topes bajos, que gasta en cualquier entorno nuevo sin decisión.
12. **Los topes de 20 000 y 200 000 tokens son valores iniciales sin aprobar.** Quedan como configuración; la decisión del monto es de José Julián (pregunta 6 del plan del sprint).

## 15. Riesgos y dudas abiertas
- **El gasto en tokens no está autorizado todavía:** el ADR-008 lo deja a TT-IA-01 y esta spec fija topes, pero el monto real y la tarifa se confirman al desplegar. Mientras no haya recurso, todo responde con plantilla.
- **El filtro no es perfecto:** reconoce formas conocidas de teléfono, CURP, RFC, correo, tarjeta y dirección; un dato escrito de forma rara puede pasar. La defensa en profundidad es que tampoco se envían nombres ni identificadores de la cuenta.
- **La calidad de las plantillas es limitada:** son reglas, no un modelo; en la demo se nota la diferencia. Se acepta porque el respaldo debe existir.
- **La cuota gratuita puede cambiar:** si el 11 de noviembre el despliegue falla por cuota, el ADR-008 se revisa; esta spec no cambia.
- **El `sub` en `uso_ia` liga el consumo con una persona:** es necesario para el tope; el documento no tiene contenido y se puede borrar con la cuenta (la baja de cuenta es una historia posterior; esta spec deja el índice `{ usuarioId, creadoEn }` para poder borrarlos).
- **La retención de `uso_ia` no está decidida:** hoy no tiene TTL; se define con el primer reporte de costos.
- **Sin prueba contra el servicio real en el CI:** el contrato se prueba con el simulador; un cambio de formato del proveedor se detectaría en la prueba manual, no en el pipeline.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
