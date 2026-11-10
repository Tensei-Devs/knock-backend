# Spec 031 — Moderación de contenido con Azure

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Clave de trabajo:** KNOCK-171 (TT-IA-02) · **Sprint:** 4 (etiqueta en Jira); implementación sujeta a la decisión de gasto de Azure
> **Servicios:** Mercado y Mensajería y notificaciones (`Common/Moderation/` en cada uno) · **Colecciones:** `uso_moderacion` (nueva, en `market` y en `messaging`); llaves de Redis `mod:*`
> **Se apoya en:** la spec 013 (contrato de moderación, falla cerrada, Azure AI Content Safety) y la spec 024 (filtro de datos personales, topes en Redis, reintento y cortacircuito). No usa Azure OpenAI.

## 1. Objetivo
Que todo texto e imagen que una persona publica en Knock se revise con Azure AI Content Safety antes de guardarse, con un solo contrato para todos los servicios: lo inapropiado se rechaza con un mensaje claro, lo que no se pudo revisar no se publica, ningún dato personal sale hacia Azure, el consumo no rebasa la cuota gratuita y, sin red, una lista local rechaza lo evidente. Es la implementación real de la interfaz que la spec 013 dejó definida.

## 2. Historia y alcance
**2.1 Historia**
- **TT-IA-02 (KNOCK-171)** — Como equipo, necesitamos moderar texto e imágenes con la API de la nube antes de guardar, para que el contenido inapropiado se rechace antes de almacenarse, con prueba.

**2.2 Dentro**
- La interfaz `IContentModerator` en `Knock.Contracts/Moderation/` (spec 013) con sus tipos de entrada y resultado.
- Su implementación con Azure AI Content Safety (análisis de texto y de imagen) en `Common/Moderation/` de Mercado y de Mensajería.
- La lista local de términos prohibidos (respaldo que solo puede rechazar) y las plantillas locales de los mensajes al usuario.
- El filtro de datos personales antes de enviar texto (reglas de la spec 024).
- Umbrales por categoría en configuración, caché de decisiones, topes de consumo, reintento y cortacircuito.
- El registro de uso sin contenido y la tabla de cómo cada llamador traduce el resultado a HTTP.
- Los puntos de uso: descripción y nota (spec 020), mensajes del chat (spec 030) e imágenes públicas (spec 032).

**2.3 Fuera**
- Video: Knock no admite video (spec 013, R-07); el resumen de la tarea en Jira lo menciona, pero no se modera porque no se recibe (ver §15).
- Las fotos de verificación de identidad (spec 025): nunca se envían a moderación ni a ningún servicio de IA.
- Las rutas que suben o publican contenido: son de las specs 020, 030 y 032; aquí solo el cliente.
- La revisión humana de reportes y el panel interno (HU-066, HU-067).
- Azure OpenAI y su filtro de contenido: la IA generativa es de la spec 024.
- El recurso de Azure en Terraform: lo crea la spec 005 con el plan gratuito (ver §15).

## 3. Requisitos
- **R-01:** `Knock.Contracts/Moderation/` define, solo con tipos y sin lógica: `IContentModerator` con `ModerateTextAsync(TextModerationRequest, CancellationToken)` y `ModerateImageAsync(ImageModerationRequest, CancellationToken)`; `TextModerationRequest { Text, Context, UserId }`; `ImageModerationRequest { Content, ContentType, Context, UserId }`; y `ModerationResult { Decision, Categories[], Source, ReasonCode? }`. `Decision` es `Approved`, `Rejected` o `Unavailable`; `Source` es `azure`, `lista_local` o `cache`; `Context` es `solicitud_descripcion`, `cotizacion_nota`, `mensaje_chat` o `imagen_publica`.
- **R-02:** Cada servicio que modera (Mercado y Mensajería) registra la implementación con `AddKnockModeration()`. Ningún otro componente llama a Content Safety; una prueba de arquitectura lo verifica, y el módulo de verificación de identidad (spec 025) no depende de `IContentModerator`.
- **R-03:** **Texto, paso 1: lista local.** El texto se normaliza (minúsculas, sin acentos, sin signos repetidos, con sustituciones comunes como «4» por «a» o «0» por «o») y se compara con la lista local `lista-local.es-MX.txt` por palabra completa. Si coincide, el resultado es `Rejected` con `Source = lista_local` y no se llama a Azure.
- **R-04:** **Texto, paso 2: datos personales.** Antes de enviar, el texto pasa por las reglas del filtro de datos personales de la spec 024 (teléfonos, CURP, RFC, correos, tarjetas y direcciones) y se envía solo el texto censurado. El texto original no sale del servicio ni se registra.
- **R-05:** **Texto, paso 3: Azure.** Se llama a `text:analyze` de Content Safety con las categorías `Hate`, `Sexual`, `Violence` y `SelfHarm` y salida de cuatro niveles de severidad (0, 2, 4, 6). El resultado es `Rejected` si alguna categoría alcanza su umbral de `Moderacion:Umbrales:<Categoria>` (por omisión: `Sexual` 2, las demás 4) y `Approved` si ninguna. `Categories` lista solo las categorías que alcanzaron el umbral.
- **R-06:** El texto a moderar mide de 1 a 4 000 caracteres; uno mayor se rechaza con `ReasonCode = not_moderable` sin llamar a Azure (ningún llamador de Knock admite más de 1 000).
- **R-07:** **Imagen.** Se llama a `image:analyze` con los bytes ya procesados por la spec 032 (JPEG recodificado, sin metadatos). Solo se aceptan `image/jpeg` o `image/png` de hasta 4 MB con `Context = imagen_publica`; cualquier otra entrada es `Rejected` con `ReasonCode = not_moderable` sin llamar a Azure. Los umbrales son los mismos de R-05.
- **R-08:** **Falla cerrada.** Si Azure no responde en `Moderacion:TiempoMaximoSegundos` (5), responde 5xx o 429 tras un reintento, responde 401 o 403 (sin reintento), devuelve un cuerpo que no se puede interpretar, o el cortacircuito está abierto, el resultado es `Unavailable` con `ReasonCode = unavailable`. Nunca se devuelve `Approved` sin una respuesta válida de Azure o de la caché.
- **R-09:** Reintento único ante 429, 5xx o error de red, respetando `Retry-After` si es de 2 segundos o menos; cortacircuito por réplica que se abre 30 segundos tras 5 fallos seguidos.
- **R-10:** **Caché de decisiones.** El resultado `Approved` o `Rejected` de Azure se guarda en Redis con la huella SHA-256 del texto ya censurado y normalizado (`mod:texto:{huella}`, 24 horas) o de los bytes de la imagen (`mod:imagen:{huella}`, 30 días), junto con la versión de los umbrales. Una consulta con la misma huella y la misma versión responde desde la caché con `Source = cache`, sin gastar cuota. `Unavailable` nunca se guarda.
- **R-11:** **Topes de consumo.** Antes de llamar a Azure se cuenta la llamada en Redis con una operación atómica: tope mensual global por servicio `Moderacion:Limites:LlamadasPorMes` (`mod:consumo:{servicio}:{yyyyMM}`) y tope diario por persona `Moderacion:Limites:LlamadasPorPersonaPorDia` (300 por omisión; `mod:usuario:{sub}:{yyyyMMdd}`), con días y meses en `America/Mexico_City`. Si se agotó el mensual, el resultado es `Unavailable` con `ReasonCode = quota_exhausted`; si se agotó el de la persona, `Unavailable` con `ReasonCode = per_person_limit`. Al pasar del 80 % del mensual se emite la métrica `moderacion_cuota_80` una vez al día.
- **R-12:** Si Redis no responde, no se llama a Azure (no se puede contar ni consultar la caché) y el resultado es `Unavailable` con `ReasonCode = limit_unavailable`.
- **R-13:** **Traducción a HTTP.** Los llamadores usan `ModerationProblemMapper`, igual en todos: `Rejected` de texto → 422 `content_rejected`; `Rejected` de imagen → 422 `image_rejected` (spec 032); `Unavailable` con `per_person_limit` → 429 `moderation_limit` con `Retry-After` hasta la medianoche; cualquier otro `Unavailable` → 503 `moderation_unavailable` con `Retry-After: 30`. En ningún caso se guarda ni se publica el contenido.
- **R-14:** **Plantillas locales.** Los textos que ve la persona salen de plantillas versionadas en español de México por código, sin revelar la categoría ni el término: `content_rejected` = «Tu texto no cumple las reglas de la comunidad de Knock. Revísalo y vuelve a intentarlo.»; `image_rejected` = «Esta foto no se puede publicar. Elige otra que muestre solo el trabajo.»; `moderation_unavailable` = «No pudimos revisar tu contenido en este momento. Intenta de nuevo en unos minutos.»; `moderation_limit` = «Llegaste al límite de publicaciones de hoy. Intenta mañana.».
- **R-15:** **Modo de operación.** `Moderacion:Modo` es `azure` o `solo_local`. `solo_local` aprueba lo que pasa la lista local y solo se permite con `ASPNETCORE_ENVIRONMENT = Development`; en cualquier otro entorno el servicio no arranca con ese modo. En `azure`, si falta el punto de conexión, el servicio no arranca.
- **R-16:** **Credenciales.** En Azure se usa identidad administrada con el rol mínimo para Content Safety; si se usa clave, sale de Key Vault por configuración. La clave, el token y el punto de conexión nunca aparecen en el repositorio ni en los registros.
- **R-17:** **Registro de uso.** Cada decisión escribe en `uso_moderacion` `{ servicio, contexto, tipo, decision, origen, categorias[], reasonCode?, latenciaMs, creadoEn }`, sin texto, sin imagen, sin huella y sin `sub`; los documentos vencen a los 365 días. Los registros de la aplicación siguen la misma regla.
- **R-18:** **Lista local.** La lista vive en `Common/Moderation/lista-local.es-MX.txt` de cada servicio (una palabra o frase por línea, ya normalizada), se carga al arrancar y se valida (sin líneas vacías, sin duplicados, de 2 a 40 caracteres). Las copias de Mercado y de Mensajería deben ser idénticas; una prueba compara sus huellas.
- **R-19:** Los umbrales se validan al arrancar (solo 2, 4 o 6) y forman una versión (`Moderacion:VersionUmbrales`); cambiar un umbral sin subir la versión impide arrancar, para que la caché nunca devuelva una decisión tomada con otros umbrales.

## 4. Criterios de aceptación
- **CA-01:** Dado un texto que Azure califica con `Hate` en 4, cuando se modera, entonces el resultado es `Rejected` con `Source = azure` y `Categories = [Hate]`; y el llamador responde 422 `content_rejected` con el texto de la plantilla y no guarda nada.
- **CA-02:** Dado un texto con un término de la lista local, también escrito con acentos o con «0» en lugar de «o», cuando se modera, entonces el resultado es `Rejected` con `Source = lista_local` y no hubo ninguna llamada a Azure.
- **CA-03:** Dado un texto limpio que Azure califica con severidad 0 en todo, cuando se modera, entonces el resultado es `Approved`.
- **CA-04:** Dado un texto con teléfono, CURP, correo y dirección, cuando se modera, entonces la petición que llega al servidor simulado de Azure no contiene ninguno de esos valores.
- **CA-05:** Dado Azure con 500, con 429 dos veces, sin responder en 5 segundos o con un cuerpo que no se entiende, cuando se modera, entonces el resultado es `Unavailable` con `unavailable`, hubo como máximo un reintento, y el llamador responde 503 `moderation_unavailable` con `Retry-After` y no guarda nada.
- **CA-06:** Dado Azure con 401, cuando se modera, entonces no se reintenta, el resultado es `Unavailable` y el registro del fallo no contiene la clave ni el token.
- **CA-07:** Dado el tope mensual agotado, cuando se modera, entonces no se llama a Azure y el resultado es `Unavailable` con `quota_exhausted`; y dado el tope diario de la persona agotado, entonces es `per_person_limit` y el llamador responde 429 `moderation_limit`.
- **CA-08:** Dadas dos moderaciones simultáneas cuando solo queda una llamada en el tope mensual, cuando se ejecutan, entonces solo una llama a Azure y la otra recibe `quota_exhausted`.
- **CA-09:** Dado Redis caído, cuando se modera, entonces no se llama a Azure y el resultado es `Unavailable` con `limit_unavailable`.
- **CA-10:** Dado el mismo texto moderado dos veces en 24 horas, cuando llega la segunda, entonces responde con `Source = cache` sin llamar a Azure ni gastar cuota; y si cambió la versión de los umbrales, vuelve a llamar.
- **CA-11:** Dada una imagen JPEG que Azure califica con `Sexual` en 2, cuando se modera, entonces el resultado es `Rejected`; dada una imagen limpia, `Approved`; y dada una imagen de 5 MB o de tipo `image/gif`, `Rejected` con `not_moderable` sin llamar a Azure.
- **CA-12:** Dado un texto de 4 001 caracteres, cuando se modera, entonces es `Rejected` con `not_moderable` sin llamar a Azure.
- **CA-13:** Dado `Moderacion:Modo = solo_local` en `Production`, cuando arranca el servicio, entonces falla; en `Development` arranca y aprueba un texto limpio sin tráfico de red.
- **CA-14:** Dado un umbral de 5, o un umbral cambiado sin subir `VersionUmbrales`, cuando arranca el servicio, entonces falla.
- **CA-15:** Dadas cinco fallas seguidas de Azure en una réplica, cuando llega la sexta moderación, entonces no se llama a Azure durante 30 segundos y el resultado es `Unavailable`.
- **CA-16:** Dada una moderación de cada tipo, cuando se inspeccionan `uso_moderacion` y los registros, entonces hay un documento por decisión con contexto, decisión, origen y latencia, y ninguno contiene el texto, la imagen, la huella, el `sub`, la clave ni el punto de conexión.
- **CA-17:** Dado el código de los servicios, cuando corren las pruebas de arquitectura, entonces solo `Common/Moderation/` llama a Content Safety y el módulo de verificación de la spec 025 no depende de `IContentModerator`.
- **CA-18:** Dadas las listas locales de Mercado y de Mensajería, cuando se comparan, entonces son idénticas; y una lista con una línea vacía o duplicada impide arrancar.
- **CA-19:** Dado un texto rechazado, cuando el llamador responde, entonces el `detail` es el de la plantilla y no menciona la categoría, el término ni la severidad.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec031TextoTests.Rechaza_Por_Umbral_CA_031_01` |
| CA-02 | `Spec031ListaLocalTests.Rechaza_Sin_Llamar_A_Azure_CA_031_02` |
| CA-03 | `Spec031TextoTests.Aprueba_Texto_Limpio_CA_031_03` |
| CA-04 | `Spec031PrivacidadTests.No_Envia_Datos_Personales_CA_031_04` |
| CA-05 | `Spec031FallaCerradaTests.No_Disponible_Falla_Cerrado_CA_031_05` |
| CA-06 | `Spec031FallaCerradaTests.Sin_Reintento_En_401_CA_031_06` |
| CA-07 | `Spec031CuotaTests.Topes_Mensual_Y_Por_Persona_CA_031_07` |
| CA-08 | `Spec031CuotaTests.Conteo_Atomico_CA_031_08` |
| CA-09 | `Spec031CuotaTests.Redis_Caido_No_Llama_CA_031_09` |
| CA-10 | `Spec031CacheTests.Decision_En_Cache_CA_031_10` |
| CA-11 | `Spec031ImagenTests.Modera_Imagenes_CA_031_11` |
| CA-12 | `Spec031TextoTests.Texto_Demasiado_Largo_CA_031_12` |
| CA-13 | `Spec031ConfiguracionTests.Solo_Local_Solo_En_Desarrollo_CA_031_13` |
| CA-14 | `Spec031ConfiguracionTests.Umbrales_Validos_Y_Versionados_CA_031_14` |
| CA-15 | `Spec031FallaCerradaTests.Cortacircuito_CA_031_15` |
| CA-16 | `Spec031RegistrosTests.Uso_Sin_Contenido_CA_031_16` |
| CA-17 | `Spec031ArquitecturaTests.Un_Solo_Punto_De_Acceso_CA_031_17` |
| CA-18 | `Spec031ListaLocalTests.Listas_Identicas_Y_Validas_CA_031_18` |
| CA-19 | `Spec031PlantillasTests.Mensaje_Sin_Detalle_CA_031_19` |

## 5. Contrato
**No hay rutas públicas nuevas.** El contrato es la interfaz interna de R-01 y la traducción a HTTP de R-13, que usan las rutas de las specs 020, 030 y 032.

| Resultado | Texto (020, 030) | Imagen (032) |
|---|---|---|
| `Approved` | se guarda | se publica |
| `Rejected` | 422 `content_rejected` | 422 `image_rejected` |
| `Unavailable` + `per_person_limit` | 429 `moderation_limit` + `Retry-After` | 429 `moderation_limit` + `Retry-After` |
| `Unavailable` (otro motivo) | 503 `moderation_unavailable` + `Retry-After: 30` | 503 `moderation_unavailable` + `Retry-After` |

**Configuración:** `Moderacion:Modo`, `Moderacion:ContentSafety:Endpoint`, `Moderacion:TiempoMaximoSegundos` (5), `Moderacion:Umbrales:{Hate,Sexual,Violence,SelfHarm}`, `Moderacion:VersionUmbrales`, `Moderacion:Limites:LlamadasPorMes`, `Moderacion:Limites:LlamadasPorPersonaPorDia` (300). La credencial no es configuración del repositorio.

**Límites:** texto de 1 a 4 000 caracteres; imagen de hasta 4 MB, JPEG o PNG; 5 segundos por llamada; un reintento; cortacircuito de 30 segundos tras 5 fallos; caché de 24 horas (texto) y 30 días (imagen); 300 moderaciones por persona por día.

**Llamada saliente:** `POST {endpoint}/contentsafety/text:analyze` y `POST {endpoint}/contentsafety/image:analyze` con la versión estable de la API vigente al implementar (se fija en configuración y en `Memoria.md`).

## 6. Modelo de datos
**`uso_moderacion`** (nueva, en `market` y en `messaging`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `servicio` | string | `market` o `messaging` |
| `contexto` | string | uno de R-01 |
| `tipo` | string | `texto` o `imagen` |
| `decision` | string | `aprobado`, `rechazado`, `no_disponible` |
| `origen` | string | `azure`, `lista_local`, `cache` |
| `categorias` | string[] | solo las que alcanzaron el umbral |
| `reasonCode` | string? | `not_moderable`, `unavailable`, `quota_exhausted`, `per_person_limit`, `limit_unavailable` |
| `latenciaMs` | int | |
| `creadoEn` | date | UTC, base del TTL |

Índices: `{ creadoEn: 1 }` TTL de 365 días; `{ contexto: 1, decision: 1, creadoEn: -1 }` para el reporte.

**Redis:** `mod:texto:{huella}` (24 h), `mod:imagen:{huella}` (30 días), `mod:consumo:{servicio}:{yyyyMM}` (vence a los 40 días), `mod:usuario:{sub}:{yyyyMMdd}` (vence a las 48 h).

El script `db/` de esta spec crea `uso_moderacion` e índices en las dos bases de forma idempotente.

## 7. Arquitectura y componentes
- `Knock.Contracts/Moderation/`: solo la interfaz y los tipos de R-01 (sin lógica), como lo fija el plan de la spec 013.
- `Common/Moderation/` en Mercado y en Mensajería (copias idénticas con su marcador de excepción de duplicación, igual que `RedisExtensions` y `MongoExtensions`; plan: llevarlas a una biblioteca técnica):
  - `ContentSafetyModerator` implementa `IContentModerator` y coordina los pasos.
  - `NormalizadorDeModeracion` y `ListaLocal` (funciones puras): normalización con sustituciones y coincidencia por palabra; punto de mutación.
  - `FiltroDePrivacidad`: aplica los patrones de la spec 024 (en Mercado reutiliza `FiltroDatosPersonales`; en Mensajería, copia de los patrones con la misma excepción).
  - `ContentSafetyClient`: cliente HTTP tipado con `Azure.Identity` para el token de la identidad administrada; único punto que conoce el formato de Azure; deserializa a un tipo cerrado.
  - `ConsumoModeracionStore` (Redis) y `UsoModeracionStore` (MongoDB).
  - `ModerationProblemMapper` y `PlantillasDeModeracion` (R-13, R-14).
  - `ModerationOptions` valida modo, punto de conexión, umbrales, versión y límites al arrancar.
- `AddKnockModeration()` registra todo; `TimeProvider` para días, meses y el cortacircuito.
- La moderación no forma parte de `/health/ready`: si Azure falla, el servicio sigue atendiendo lo demás y solo las publicaciones responden 503.

## 8. Dependencias
- Spec 013 (contrato y falla cerrada), 024 (filtro de datos personales, patrón de topes y cortacircuito), 003 (MongoDB y Redis), 005 (recurso de Content Safety e identidad administrada), 009 (secretos en Key Vault), 011 (registros sin datos).
- Llamadores: spec 020 (descripción y nota), 030 (mensajes) y 032 (imágenes públicas).
- `Azure.Identity` (MIT), que también agrega la spec 032; no se agrega el SDK `Azure.AI.ContentSafety` (ver §14, decisión 3).
- En pruebas, un servidor HTTP simulado de Content Safety; ninguna prueba del CI llama al servicio real.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Publicar contenido ofensivo | Moderación antes de guardar; umbrales por categoría | CA-01, CA-11 |
| Publicar sin revisar cuando Azure falla | Falla cerrada en todos los caminos de error; sin `Approved` sin respuesta válida | CA-05, CA-06, CA-15 |
| Enviar datos personales a un tercero | Filtro de la spec 024 antes de enviar; fotos de verificación excluidas | CA-04, CA-17 |
| Agotar la cuota gratuita (abuso o error) | Tope mensual y por persona con conteo atómico; caché de decisiones | CA-07, CA-08, CA-10 |
| Gastar sin poder contar | Sin Redis no se llama | CA-09 |
| Evadir la lista con acentos o números | Normalización con sustituciones | CA-02 |
| Aprender qué término se bloquea | Mensaje genérico de plantilla, sin categoría ni término | CA-19 |
| Desactivar la moderación por configuración en producción | `solo_local` solo en desarrollo; sin punto de conexión no arranca | CA-13 |
| Caché con decisiones de otros umbrales | Versión de umbrales en la clave y validación al arrancar | CA-10, CA-14 |
| Fuga de la clave o del punto de conexión | Identidad administrada o Key Vault; nunca en registros | CA-06, CA-16 |
| Llamadas a Azure dispersas sin control | Un solo punto de acceso verificado por arquitectura | CA-17 |
| Listas distintas entre servicios | Prueba de huellas idénticas | CA-18 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Normalizador, lista local, filtro de privacidad, interpretación de severidades, traductor a HTTP, plantillas y validación de opciones |
| Integración | Sí | Redis y MongoDB reales con `Testcontainers` y servidor simulado de Content Safety: caché, topes atómicos, registro de uso, cortacircuito con `TimeProvider` falso |
| Seguridad negativa | Sí | Datos personales en la petición saliente, 401 sin reintento, registros sin clave ni contenido, `solo_local` en producción |
| Contrato | Sí | Los OpenAPI de Mercado y de las rutas llamadoras declaran 422, 429 y 503 con sus códigos; el formato de Azure se fija con respuestas grabadas en el simulador |
| Arquitectura | Sí | Un solo punto de acceso; verificación sin moderador; `Knock.Contracts` sin lógica |
| Base de datos | Sí | Script idempotente de `uso_moderacion` con TTL en las dos bases |
| Mutación | Sí | Normalizador, lista local y comparación con umbrales (meta 70 %) |
| Humo y extremo a extremo | Sí | En el entorno de evaluación con el moderador real: un texto sintético aprobado y uno rechazado por el flujo de la spec 020 (201 o 422 `content_rejected`), y el tope diario de la cuenta de prueba (429 `moderation_limit`) |
| Regresión | Sí | Las pruebas de moderación de las specs 020, 030 y 032 con el moderador real contra el simulador |
| Rendimiento | No | La latencia la domina Azure; la caché reduce llamadas |
| CI/CD y análisis | Sí | Semgrep, Gitleaks, Trivy y dependencias; `Azure.Identity` evaluada y registrada |
| Aceptación | Sí | CA-01 a CA-19 con la tabla 4.1 |

**Prueba manual (no entra al CI):** una llamada real por tipo contra el recurso desplegado, con texto sintético y una imagen sintética, para confirmar el formato; se anota en el PR.

**Gates:** los de `AGENTS.md`; `python scripts/sdd_lint.py --trace 031`.

## 11. Escenarios
**Positivos**
- «Se me rompió la tubería del baño» se aprueba y la solicitud se crea.
- Una foto de una llave que gotea se aprueba y se publica.
- El mismo «hola, ¿a qué hora puede venir?» enviado por diez personas consume una sola llamada.

**Negativos**
- Un insulto escrito con números en lugar de letras: lo rechaza la lista local.
- Un texto con el teléfono del cliente: Azure recibe «[dato oculto]».
- Una persona que publica 400 mensajes en un día: al llegar a 300 recibe 429.

**Errores esperados**
- 422 `content_rejected` o `image_rejected`, 429 `moderation_limit`, 503 `moderation_unavailable`, siempre sin guardar.

**Casos límite**
- Severidad exactamente igual al umbral (se rechaza); texto de 4 000 y 4 001 caracteres; imagen de 4 MB exactos; el cambio de mes con el tope agotado; Azure responde 429 con `Retry-After: 1` (se reintenta) y con `Retry-After: 10` (no se espera: `Unavailable`); una palabra prohibida dentro de otra palabra permitida (no coincide: es por palabra completa).

## 12. Definición de terminado
- Los 19 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales.
- Las rutas de las specs 020, 030 y 032 usan `IContentModerator` y `ModerationProblemMapper`, y sus pruebas de falla cerrada pasan con el moderador real contra el simulador.
- `db/` crea `uso_moderacion` en `market` y `messaging`; `Memoria.md` registra la versión de la API de Azure, `Azure.Identity`, la excepción de duplicación y la decisión de umbrales.
- La prueba manual contra Azure está anotada en el PR.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Content Safety y no Azure OpenAI:** la spec 013 ya fijó Content Safety; es el servicio específico para moderar, tiene plan gratuito y no genera texto. El «cliente de la spec 024» se reutiliza en sus reglas (filtro, topes, falla cerrada), no en el proveedor.
- **La lista local solo puede rechazar:** si pudiera aprobar, una caída de Azure abriría la puerta a todo lo que la lista no conoce; la constitución exige no publicar sin revisión.
- **Caché de decisiones:** muchos mensajes del chat se repiten; la caché ahorra cuota sin cambiar la decisión, porque la clave incluye la versión de los umbrales.
- **Sin la moderación en `/health/ready`:** una caída de Azure no debe sacar de rotación al servicio; solo deben fallar las publicaciones.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **Umbrales por omisión: `Sexual` en 2 y las demás categorías en 4.** Un servicio de oficios no necesita contenido sexual ni siquiera leve; las otras categorías en 2 rechazarían descripciones normales («se quemó el cable», «golpe en la defensa»). Alternativa: 4 en todas.
2. **La implementación se copia en Mercado y Mensajería con excepción de duplicación**, como fija el plan de la spec 013 (`<Servicio>/Common/Media/`) y como ya se hizo con Redis y MongoDB. Alternativa: una biblioteca técnica compartida nueva, que es el plan de largo plazo pero agrega un proyecto ahora.
3. **Llamada por REST con `HttpClient` y `Azure.Identity`, sin el SDK de Content Safety.** Son dos operaciones y el formato es pequeño; un paquete menos que auditar. Alternativa: `Azure.AI.ContentSafety` (MIT), que se puede adoptar si el formato cambia.
4. **Tope de 300 moderaciones por persona por día.** Cubre con holgura el uso real (30 mensajes por minuto solo en ráfagas) y evita que una persona agote la cuota de todos. Alternativa: sin tope por persona.
5. **Imágenes de verificación excluidas de la moderación.** No se publican y la constitución prohíbe que la IA reciba datos sensibles; la revisión es humana (spec 025).
6. **El texto se modera ya censurado.** Se pierde la posibilidad de moderar el dato personal en sí, que no aporta a la decisión; a cambio, ningún teléfono ni CURP sale hacia Azure.
7. **`solo_local` como modo de desarrollo.** Permite trabajar sin recurso de Azure y sin gastar; en cualquier otro entorno impide arrancar para que nadie lo deje encendido por error.
8. **Mensaje al usuario genérico, sin categoría.** Mostrar la categoría ayuda a reformular, pero también a evadir; se prefiere el mensaje genérico. Alternativa: incluir la categoría en términos amables.

## 15. Riesgos y dudas abiertas
- **Conflicto con el resumen de Jira (KNOCK-171):** dice «texto, imágenes y videos»; la spec 013 (Aprobada) prohíbe video, así que aquí no se modera video. Hay que corregir el resumen o la 013.
- **Cuota del plan gratuito sin confirmar:** el número de llamadas al mes del plan gratuito de Content Safety se confirma en el portal al crear el recurso; `LlamadasPorMes` debe quedar por debajo. Con la cuota agotada nadie puede publicar (falla cerrada), por eso la métrica del 80 %.
- **Recurso no creado:** si la suscripción de estudiante no permite el plan gratuito en la región del backend, la decisión de gasto la toma el líder (como en el ADR-008); mientras tanto, en entornos desplegados las publicaciones responden 503.
- **Límites del servicio por confirmar:** tamaño máximo de texto e imagen y versión de la API se verifican con la documentación oficial al implementar y se anotan en `Memoria.md`.
- **La lista local es corta y en español:** la defensa principal es Azure; la lista se amplía con los rechazos de Azure que la revisión humana confirme (fuera de esta spec).
- **El nombre del perfil (spec 017, Aprobada) no se modera:** la constitución pide moderar todo texto que sube una persona; se anota para una revisión de la 017.
- **Duplicación entre servicios:** hasta llevarla a una biblioteca técnica, un cambio en la moderación se hace dos veces; la prueba de listas idénticas solo cubre la lista.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
