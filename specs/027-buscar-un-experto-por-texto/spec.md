# Spec 027 — Buscar un experto por texto

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Clave de trabajo:** KNOCK-66 (HU-012) · **Sprint:** 5 (la spec se deja lista en el Sprint 4)
> **Servicio:** Mercado · **Módulo:** Catalog · **Colecciones:** `sinonimos` (nueva), `busquedas_sin_resultado` (nueva), `tecnicos` y `colonias` (lectura)
> **Otra parte en:** knock-frontend, spec 021 «Buscar un experto por texto» (consume `GET /api/v1/busqueda`)

## 1. Objetivo
Que una persona que no sabe qué oficio elegir escriba con sus palabras («boiler», «frenos», «se fue la luz», el nombre de un negocio) y llegue a los técnicos correctos de su zona. La búsqueda detecta el oficio por palabras y sinónimos, tolera acentos y errores de dedo, y, si no entiende, lo dice para que la persona elija el oficio a mano. Nunca expone datos de contacto ni la ubicación exacta de un técnico.

## 2. Historia y alcance
**2.1 Historia**
- **HU-012 (KNOCK-66)** — Como persona que no sabe qué categoría elegir, quiero buscar con mis palabras («boiler», «frenos»), para llegar a los expertos correctos.

**2.2 Dentro**
- `GET /api/v1/busqueda`: detección del oficio, coincidencia por nombre del técnico o de su negocio, filtros, orden y paginación por cursor.
- Diccionario de sinónimos por oficio (colección `sinonimos`), sembrado y versionado en `db/`.
- Normalización del texto (minúsculas, sin acentos, sin signos) y tolerancia a errores de dedo.
- Registro agregado y anónimo de los términos sin resultado, para mejorar el diccionario.
- Límite de peticiones propio de la búsqueda y tope de profundidad de páginas contra la extracción masiva.

**2.3 Fuera**
- El asistente conversacional con IA (HU-057): esta spec no usa IA ni llama a Azure OpenAI.
- El mapa de expertos cercanos (HU-011) y el perfil público del técnico (HU-013): solo se devuelve el identificador para abrirlo.
- El orden «Recomendadas» con aprendizaje automático (HU-060).
- La edición del diccionario desde un panel: se cambia con un script de `db/` revisado en un PR.
- La pantalla de búsqueda (frontend 021).
- Dar de alta o editar técnicos: esta spec solo lee `tecnicos` (ver §15, hueco de la ficha del técnico).

## 3. Requisitos
- **R-01:** `GET /api/v1/busqueda` exige JWT de persona y acepta en la consulta `q` (obligatorio), `coloniaId` (obligatorio, `ObjectId`), `oficio` (opcional, clave del catálogo), `soloVerificados` (opcional, `true` o `false`, por omisión `false`), `cursor` (opcional) y `limit` (opcional, de 1 a 20, por omisión 20). Un parámetro repetido o desconocido no cambia el resultado: los repetidos responden 422 y los desconocidos se ignoran.
- **R-02:** `q` se normaliza antes de validar: se recorta, se pasa a minúsculas, se quitan acentos y diéresis (la «ñ» se conserva), se reemplazan los signos por espacios y se juntan los espacios repetidos. El texto normalizado debe medir de 2 a 80 caracteres y contener solo letras, dígitos y espacios; si no, responde 422 con `errors.q`. El texto original no se guarda ni se registra.
- **R-03:** `coloniaId` debe existir y estar activa en `colonias`; si no, responde 422 `colonia_not_found`. `oficio`, si viene, debe existir y estar activo; si no, 422 `trade_invalid`. Las dos validaciones usan la caché del catálogo (24 h, spec 010).
- **R-04:** La detección del oficio divide `q` en palabras y compara cada palabra y cada par de palabras seguidas con `sinonimos.termino` y con el nombre normalizado de cada oficio activo. Primero busca la coincidencia exacta; si no hay, acepta una distancia de edición de Damerau-Levenshtein de 1 para palabras de 4 a 7 letras y de 2 para palabras de 8 letras o más (las de 3 letras o menos solo cuentan exactas). El oficio con más palabras coincidentes gana; un empate entre oficios distintos se informa como ambiguo.
- **R-05:** La respuesta incluye `interpretacion` con `estado`: `detectado` (un oficio, con `oficioDetectado { clave, nombre }`), `ambiguo` (dos o más, con `oficiosSugeridos[]` de hasta 3) o `no_entendido` (ninguno). Con `oficio` en la consulta, el filtro manda y `interpretacion.estado` es `elegido`. En `ambiguo` y `no_entendido` la lista de técnicos solo trae las coincidencias por nombre de R-06, y la app ofrece elegir el oficio.
- **R-06:** Además del oficio, `q` se compara con `tecnicos.palabrasBusqueda` (palabras normalizadas del nombre público y del nombre del negocio): una palabra de `q` de 3 letras o más coincide si es prefijo de alguna de esas palabras. Un técnico que coincide por nombre aparece aunque su oficio no sea el detectado, siempre que cumpla R-07 y atienda la colonia.
- **R-07:** Solo aparecen técnicos con `estado = activo`, `oculto = false` y al menos un oficio activo; nunca los suspendidos, ocultos por un reporte, dados de baja o en eliminación programada. Con `soloVerificados = true` solo aparecen los de `nivelVerificacion` distinto de `ninguno`.
- **R-08:** Un técnico «atiende la colonia» si la distancia entre su `ubicacionBase` (centro de su colonia base, nunca su domicilio) y el `centro` de la colonia consultada es menor o igual que su `radioAtencionKm`. Si la colonia consultada no tiene `centro`, solo atienden los técnicos cuya colonia base es esa misma colonia. Los técnicos que no atienden la colonia no aparecen, salvo los de coincidencia por nombre, que aparecen al final con `atiendeTuColonia = false`.
- **R-09:** El orden es determinista: 1) coincidencia por nombre que además atiende la colonia; 2) técnicos del oficio que atienden la colonia; 3) coincidencias por nombre que no la atienden. Dentro de cada grupo: verificados primero, luego `calificacion` de mayor a menor, luego distancia de menor a mayor y al final el `_id` como desempate.
- **R-10:** Cada elemento trae solo datos públicos: `{ id, nombre, oficios: [{ clave, nombre }], calificacion, totalResenas, verificado, nivelVerificacion, coloniaBase, distanciaKm, atiendeTuColonia, fotoUrl }`. `distanciaKm` es un entero aproximado entre los centros de las dos colonias (mínimo 1). Nunca se devuelven el `usuarioId` (`sub`), el teléfono, el contacto público, la dirección, la ubicación ni el radio de atención.
- **R-11:** La paginación es por cursor (spec 010): la respuesta trae `nextCursor` cuando hay más resultados. El cursor es opaco (base64url) y contiene la última clave de orden y una huella de la consulta normalizada (`q`, `coloniaId`, `oficio`, `soloVerificados`) y de la versión del diccionario de sinónimos vigente (así una actualización del diccionario entre dos páginas no repite ni omite técnicos; la app reinicia desde la primera página); un cursor mal formado, alterado, de otra consulta o de otra versión del diccionario responde 400 `invalid_cursor`. Una misma consulta devuelve como máximo 100 técnicos en total (5 páginas de 20); después, `nextCursor` es nulo.
- **R-12:** Las búsquedas sin ningún técnico ni oficio detectado se registran en `busquedas_sin_resultado` solo de forma agregada: `{ terminoNormalizado, dia, conteo }`, sin `sub`, sin colonia y sin IP, con incremento atómico (`upsert` con `$inc`). Antes de guardar, el término pasa por el filtro de datos personales de la spec 024; si contiene un dato personal (teléfono, correo, CURP, RFC, tarjeta o dirección) o 7 dígitos o más seguidos, no se guarda. Los documentos vencen a los 90 días (índice TTL).
- **R-13:** La respuesta se cachea en Redis 30 segundos con la clave `busqueda:{huella}`, donde la huella es un SHA-256 de la consulta normalizada y del número de versión del diccionario; no depende de quién pregunta porque el resultado no es personal. La respuesta lleva `Cache-Control: private, max-age=30`. Si Redis no responde, la búsqueda va a MongoDB y responde igual. Al cambiar el diccionario sube su versión (`sinonimos_version`) y las claves anteriores dejan de usarse.
- **R-14:** Límite propio de 30 búsquedas por minuto por persona, además de los límites global y por usuario del gateway (spec 009). Se aplica en el gateway como política de ruta `busqueda` con contador por `sub` en Redis (mismo script atómico de la spec 009); al pasarse responde 429 con `Retry-After`. Si Redis no responde, el gateway responde 503, como en el resto de sus límites.
- **R-15:** Rendimiento: con 200 técnicos activos y el diccionario sembrado, la búsqueda responde en menos de 300 ms en el percentil 95 sin caché, con las consultas indexadas de §6 y la proyección mínima de R-10.
- **R-16:** Si MongoDB no responde, responde 503 `search_unavailable`. Los errores usan ProblemDetails con `correlationId` y `traceId`. Los registros llevan solo conteos, el estado de la interpretación y la latencia; nunca `q`, el término normalizado, la colonia ni el `sub`.

## 4. Criterios de aceptación
- **CA-01:** Dada una persona en una colonia atendida por tres plomeros, cuando busca «boiler», entonces `interpretacion.estado` es `detectado` con plomería y recibe a los tres plomeros que atienden su colonia.
- **CA-02:** Dada la búsqueda «frenso» (con error), cuando se busca, entonces se detecta mecánica; y dada «Bóiler», «BOILER» o «boiler!!», cuando se busca, entonces el resultado es igual al de «boiler».
- **CA-03:** Dado un texto que no corresponde a ningún oficio ni a ningún nombre («quiero pastel»), cuando se busca, entonces responde 200 con `interpretacion.estado = no_entendido`, lista vacía y sin `nextCursor`, y el término queda contado en `busquedas_sin_resultado` sin el `sub`.
- **CA-04:** Dado un técnico activo cuyo negocio se llama «Taller El Güero», cuando se busca «guero», entonces aparece primero; si ese técnico está suspendido u oculto, entonces no aparece.
- **CA-05:** Dado un texto que coincide con dos oficios por igual («luz agua»), cuando se busca, entonces `interpretacion.estado` es `ambiguo` con los dos oficios en `oficiosSugeridos`.
- **CA-06:** Dado `oficio=electricidad` en la consulta, cuando se busca «boiler», entonces manda el filtro: `interpretacion.estado` es `elegido` y solo hay electricistas (o coincidencias por nombre).
- **CA-07:** Dado `soloVerificados=true`, cuando se busca, entonces ningún elemento tiene `verificado = false`.
- **CA-08:** Dados técnicos con distintas calificaciones, verificación y distancia, cuando se busca, entonces el orden sigue R-09 y es el mismo en dos peticiones iguales.
- **CA-09:** Dado un técnico cuyo radio no llega a la colonia consultada, cuando se busca su oficio, entonces no aparece; y dada una colonia sin `centro`, cuando se busca, entonces solo aparecen técnicos con esa colonia base.
- **CA-10:** Dados 45 técnicos que cumplen, cuando se pide la primera página, entonces recibe 20 y un `nextCursor`; con el cursor recibe los siguientes 20 sin repetir ninguno; y dados 130, cuando se recorren las páginas, entonces la quinta no trae `nextCursor` (tope de 100).
- **CA-11:** Dado un cursor alterado, de otra consulta o de otra versión del diccionario, cuando se busca, entonces responde 400 `invalid_cursor`.
- **CA-12:** Dado `q` de 1 carácter, de 81 caracteres, solo con signos, repetido en la consulta, o `limit=21`, cuando se busca, entonces responde 422 con `errors`; y dada una `coloniaId` inexistente o un `oficio` inactivo, entonces responde 422.
- **CA-13:** Dado cualquier resultado, cuando se inspecciona el cuerpo, entonces no contiene `usuarioId`, teléfono, contacto público, dirección, coordenadas ni `radioAtencionKm`.
- **CA-14:** Dada una búsqueda sin resultado con un teléfono, un correo o 7 dígitos seguidos, cuando se registra, entonces no se guarda ningún documento en `busquedas_sin_resultado`.
- **CA-15:** Dada la misma búsqueda dos veces en 30 segundos, cuando llega la segunda, entonces no consulta MongoDB y lleva `Cache-Control: private, max-age=30`; con Redis caído responde igual desde MongoDB.
- **CA-16:** Dada una petición sin token, cuando se busca, entonces responde 401; y dadas 31 búsquedas en un minuto de la misma persona, cuando llega la 31, entonces responde 429 con `Retry-After`.
- **CA-17:** Dado MongoDB caído, cuando se busca, entonces responde 503 `search_unavailable`.
- **CA-18:** Dados el registro de una búsqueda con resultado y otra sin resultado, cuando se inspeccionan, entonces no contienen `q`, el término normalizado, la colonia ni el `sub`.
- **CA-19:** Dados 200 técnicos activos y el diccionario sembrado, cuando se ejecutan 200 búsquedas variadas sin caché, entonces el percentil 95 es menor de 300 ms.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec027BusquedaTests.Detecta_Oficio_Por_Sinonimo_CA_027_01` |
| CA-02 | `Spec027NormalizacionTests.Tolera_Errores_Y_Acentos_CA_027_02` |
| CA-03 | `Spec027BusquedaTests.No_Entendido_Y_Registro_Anonimo_CA_027_03` |
| CA-04 | `Spec027BusquedaTests.Coincide_Por_Nombre_Solo_Activos_CA_027_04` |
| CA-05 | `Spec027InterpretacionTests.Informa_Ambiguedad_CA_027_05` |
| CA-06 | `Spec027InterpretacionTests.Filtro_Manda_Sobre_Deteccion_CA_027_06` |
| CA-07 | `Spec027FiltrosTests.Solo_Verificados_CA_027_07` |
| CA-08 | `Spec027OrdenTests.Orden_Determinista_CA_027_08` |
| CA-09 | `Spec027CoberturaTests.Respeta_Radio_Y_Colonia_Sin_Centro_CA_027_09` |
| CA-10 | `Spec027PaginacionTests.Cursor_Y_Tope_De_Cien_CA_027_10` |
| CA-11 | `Spec027PaginacionTests.Rechaza_Cursor_Alterado_CA_027_11` |
| CA-12 | `Spec027ValidacionTests.Rechaza_Consulta_Invalida_CA_027_12` |
| CA-13 | `Spec027PrivacidadTests.Sin_Datos_Privados_CA_027_13` |
| CA-14 | `Spec027PrivacidadTests.No_Guarda_Terminos_Con_Datos_Personales_CA_027_14` |
| CA-15 | `Spec027CacheTests.Cache_De_Treinta_Segundos_CA_027_15` |
| CA-16 | `Spec027SeguridadTests.Exige_Sesion_Y_Limite_CA_027_16` |
| CA-17 | `Spec027SaludTests.Mongo_Caido_Responde_503_CA_027_17` |
| CA-18 | `Spec027RegistrosTests.Sin_Texto_Ni_Sub_CA_027_18` |
| CA-19 | `Spec027RendimientoTests.Percentil_95_Bajo_300ms_CA_027_19` |

## 5. Contrato
| Método y ruta | Quién | Consulta | Respuestas |
|---|---|---|---|
| `GET /api/v1/busqueda` | persona con sesión | `q`, `coloniaId`, `oficio?`, `soloVerificados?`, `cursor?`, `limit?` (1–20) | 200 `ResultadoBusqueda`, 400 (`invalid_cursor`), 401, 422 (formato y catálogo), 429, 503 |

**Cuerpo de respuesta (`ResultadoBusqueda`)**
```
{
  interpretacion: { estado: "detectado" | "ambiguo" | "no_entendido" | "elegido",
                    oficioDetectado?: { clave, nombre }, oficiosSugeridos?: [{ clave, nombre }] },
  items: [{ id, nombre, oficios: [{ clave, nombre }], calificacion, totalResenas, verificado,
            nivelVerificacion, coloniaBase, distanciaKm, atiendeTuColonia, fotoUrl }],
  nextCursor: string | null
}
```

**Encabezados:** `Cache-Control: private, max-age=30` en 200; `Retry-After` en 429; `X-Correlation-Id` en todas.

**Códigos de error:** `colonia_not_found` (422), `trade_invalid` (422), `invalid_cursor` (400), `search_unavailable` (503). Los errores de forma salen como 422 con `errors` por campo (`q`, `limit`, `soloVerificados`, `coloniaId`).

**Límites:** `q` de 2 a 80 caracteres normalizados; `limit` de 1 a 20; 100 resultados como máximo por consulta; 30 búsquedas por minuto por persona; caché de 30 segundos.

**Idempotencia:** es una lectura sin efectos sobre los datos de la persona; el único efecto es el conteo anónimo de R-12, que no cambia la respuesta.

## 6. Modelo de datos
**`sinonimos`** (nueva, base `market`, módulo Catalog)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `termino` | string | normalizado como R-02; una o dos palabras |
| `oficioClave` | string | clave del catálogo de oficios |
| `version` | int | versión del diccionario en que entró |

Índices: `{ termino: 1, oficioClave: 1 }` único; `{ oficioClave: 1 }`. Documento de control `sinonimos_version` en la colección `catalogo_meta` (`{ _id: "sinonimos", version }`). Semilla inicial en `db/seeds/catalog/sinonimos.json` (por ejemplo: plomería ← «boiler», «calentador», «fuga», «tinaco», «tuberia», «llave»; electricidad ← «luz», «apagon», «contacto», «cortocircuito», «foco»; mecánica ← «frenos», «auto», «carro», «motor», «afinacion», «llanta»).

**`busquedas_sin_resultado`** (nueva, base `market`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `terminoNormalizado` | string | ya filtrado (R-12) |
| `dia` | string | `YYYY-MM-DD` en `America/Mexico_City` |
| `conteo` | int | incremento atómico |
| `creadoEn` | date | UTC, base del TTL |

Índices: `{ terminoNormalizado: 1, dia: 1 }` único; `{ creadoEn: 1 }` TTL de 90 días.

**`tecnicos`** (lectura; campos que esta spec necesita)
| Campo | Uso aquí |
|---|---|
| `_id` | `id` del resultado |
| `nombrePublico`, `fotoUrl` | respuesta |
| `palabrasBusqueda` | string[] normalizado del nombre público y del negocio (R-06) |
| `oficios` | string[] de claves |
| `estado`, `oculto` | filtro de R-07 |
| `nivelVerificacion` | `ninguno`, `basica`, `completa`; `verificado` = distinto de `ninguno` |
| `calificacion`, `totalResenas` | orden y respuesta |
| `coloniaBaseId`, `coloniaBaseNombre` | colonia base y caso sin `centro` |
| `ubicacionBase` | GeoJSON `Point` del centro de la colonia base; nunca se devuelve |
| `radioAtencionKm` | entero de 1 a 30; nunca se devuelve |

Índices que agrega el script de esta spec: `{ ubicacionBase: "2dsphere" }`, `{ oficios: 1, estado: 1, oculto: 1 }`, `{ palabrasBusqueda: 1 }`. El script no cambia documentos.

**`colonias`** (existente, de TT-BD-06): se leen `_id`, `nombre`, `centro` y el estado activo.

## 7. Arquitectura y componentes
- Se amplía el módulo `Catalog` existente; no se crea módulo nuevo porque ya es dueño de oficios, colonias y su caché.
- `BusquedaController` recibe y valida el formato con `BusquedaQueryValidator` (FluentValidation) y delega; no toca MongoDB ni Redis.
- `NormalizadorDeTexto` es una función pura (R-02) que reutiliza el `SearchNameNormalizer` existente del módulo y lo amplía con la «ñ» y los signos; no se crea un normalizador paralelo.
- `InterpretadorDeConsulta` es una función pura que recibe las palabras y el diccionario y devuelve la interpretación (R-04, R-05); calcula la distancia de Damerau-Levenshtein con tope para cortar pronto. Es el punto de mutación.
- `BusquedaService` coordina: interpretación, consulta de candidatos, cobertura, orden (`OrdenDeResultados`, función pura), página y cursor (`CursorDeBusqueda`, que serializa y valida la huella).
- `BusquedaStore` es el único punto con acceso a `tecnicos`, `sinonimos` y `busquedas_sin_resultado`; consulta candidatos con `$geoNear` sobre `ubicacionBase` desde el centro de la colonia, con un radio máximo de 30 km y la proyección mínima, y filtra `distancia <= radioAtencionKm` en la misma tubería.
- El filtro de datos personales de R-12 es el `FiltroDatosPersonales` de la spec 024; se usa por su interfaz y no se copia.
- La caché de resultados y del diccionario usa el cliente de Redis de la spec 003; falla en silencio (la caché es optimización).
- Sin estado en el proceso: el diccionario se cachea en Redis, no en memoria de una copia.

## 8. Dependencias
- Spec 002 (estructura y estilo), 003 (MongoDB, Redis), 009 (JWT y límites), 010 (caché y cursor), 011 (registros), 023 (colonias y su `centro`), 024 (filtro de datos personales).
- La colección `tecnicos` no tiene spec dueña todavía (ver §15): esta spec define solo los campos que lee; las semillas de demo (KNOCK-246) deben llenarlos.
- Frontend 021 consume la ruta; su contrato propuesto se ajusta a este (ver §14, decisión 7).
- La spec 028 reutiliza `ubicacionBase`, `radioAtencionKm`, `estado` y `oculto` con el mismo significado.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Extraer el padrón completo de técnicos | Sesión obligatoria, 30 búsquedas por minuto, 20 por página y 100 por consulta | CA-10, CA-16 |
| Conocer la ubicación o el domicilio de un técnico | Solo distancia entera entre centros de colonia; nunca coordenadas, radio ni dirección | CA-13 |
| Obtener el teléfono antes de elegir una cotización | La respuesta no trae contacto; el contacto solo se revela al elegir (spec 021) | CA-13 |
| Enumerar técnicos suspendidos u ocultos | Filtro de estado en la consulta; no se distingue «no existe» de «está oculto» | CA-04 |
| Manipular el cursor para saltar el tope o leer otra consulta | Cursor con huella de la consulta y tope de 100; 400 si no cuadra | CA-11, CA-10 |
| Guardar datos personales escritos en la búsqueda | Registro agregado, sin `sub`, filtrado con la spec 024 y con TTL | CA-14, CA-03 |
| Consultas costosas (texto largo, expresiones regulares) | `q` normalizado a letras, dígitos y espacios; prefijos anclados e indexados; tope de 80 | CA-12, CA-19 |
| Caché compartida que filtre datos de otra persona | La clave no depende de la persona y el resultado no contiene datos personales | CA-15 |
| Datos personales en los registros | Solo conteos, estado y latencia | CA-18 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Normalizador, interpretador (exacto, distancia 1 y 2, ambiguo), orden, cursor y huella |
| Integración | Sí | MongoDB (`documentdb-local`) y Redis reales con `Testcontainers`: `$geoNear`, cobertura, paginación, caché y conteo anónimo |
| Seguridad negativa | Sí | Sin token, límite, cursor alterado, parámetros repetidos, técnicos suspendidos u ocultos y cuerpo sin datos privados |
| Contrato | Sí | `docs/openapi.v1.json` de Mercado regenerado; `oasdiff` sin cambios incompatibles |
| Base de datos | Sí | El script crea `sinonimos`, `busquedas_sin_resultado` e índices de forma idempotente; la prueba los compara |
| Arquitectura | Sí | El controller no accede a MongoDB ni a Redis; el conjunto evaluado no está vacío |
| Mutación | Sí | `InterpretadorDeConsulta` y `OrdenDeResultados` (meta 70 %, spec 008) |
| Rendimiento | Sí | CA-19 con `[Trait("Category", "Performance")]`, sin cobertura (Memoria, KNOCK-203) |
| Humo y extremo a extremo | Sí | Flujo en el entorno de evaluación con datos de semilla: texto («boiler») → resultados → elegir técnico → solicitud con el oficio, desde el gateway y con la política de 30 por minuto activa |
| Regresión | Sí | `GET /api/v1/oficios` y `GET /api/v1/colonias/*` siguen iguales tras ampliar el normalizador |
| CI/CD y análisis | Sí | Semgrep, Gitleaks, Trivy y dependencias del CI (spec 007) sin hallazgos nuevos; no se agregan paquetes |
| Aceptación | Sí | CA-01 a CA-19 con la tabla 4.1 |

**Gates:** los de `AGENTS.md`; `python scripts/sdd_lint.py --trace 027` sin faltantes al cerrar la implementación.

## 11. Escenarios
**Positivos**
- «boiler» en una colonia con plomeros: plomería detectada y lista ordenada.
- «frenso»: mecánica detectada pese al error.
- «taller el guero»: el negocio aparece primero.
- Segunda página con el cursor de la primera.

**Negativos**
- Sin sesión; 31 búsquedas en un minuto; cursor de otra consulta; `q` repetido.
- Técnico suspendido que coincide por nombre: no aparece.
- Persona que intenta leer 500 técnicos paginando: se corta en 100.

**Errores esperados**
- 400 (`invalid_cursor`), 401, 422 (`errors.q`, `errors.limit`, `colonia_not_found`, `trade_invalid`), 429 con `Retry-After`, 503 `search_unavailable`.

**Casos límite**
- `q` de 2 y de 80 caracteres; «ñ» conservada («baño»); palabras de 3 letras (solo exactas) y de 4 y 8 letras (distancia 1 y 2).
- Empate exacto entre oficios; colonia sin `centro`; técnico con radio exactamente igual a la distancia (sí atiende).
- Exactamente 20, 21 y 100 resultados; diccionario que cambia de versión entre dos páginas (el cursor sigue siendo válido porque la huella no incluye la versión; la caché sí).

## 12. Definición de terminado
- Los 19 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales.
- `db/` crea colecciones, índices y la semilla de sinónimos de forma idempotente, y `db/entrega.js` la aplica.
- OpenAPI de Mercado regenerado, `docs/endpoints.md` con la fila nueva y `docs/rate-limit-matrix.md` con el límite de búsqueda.
- `Memoria.md` con la entrada del cambio; sin dependencias nuevas.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Búsqueda en MongoDB, no en PostgreSQL:** la ficha de HU-012 nombra `unaccent` y `pg_trgm`, pero la constitución fija MongoDB; la normalización previa y los prefijos indexados dan el mismo efecto sin un motor nuevo.
- **Tolerancia a errores en el diccionario, no en los técnicos:** la distancia de edición se calcula contra unos cientos de términos del diccionario; contra todos los nombres sería costosa y daría coincidencias falsas.
- **Sin IA:** detectar el oficio por palabras es suficiente para la historia, no cuesta y no envía texto a terceros; el asistente con IA es HU-057.
- **Resultado no personal y cacheable:** la respuesta solo depende de la consulta, así que se puede cachear unos segundos (spec 010, R-04) sin riesgo de mezclar personas.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **`coloniaId` es obligatorio.** Sin colonia no hay «expertos cercanos» y la ficha pide resultados de la zona. Alternativa: tomar la colonia del perfil (spec 017), que obliga a una llamada a Identidad por búsqueda.
2. **Se devuelve `distanciaKm` aproximada entre centros de colonia.** La pide frontend 021 y no revela nada que la colonia base no diga ya. La spec 021 decidió no devolver distancia en las cotizaciones porque no guarda la ubicación del cliente; aquí la referencia es la colonia que la persona elige, no su ubicación. Alternativa: omitirla y mostrar solo «atiende tu colonia».
3. **Tope de 100 resultados por consulta.** Limita la extracción masiva sin afectar el uso real (nadie revisa más de cinco páginas). Alternativa: sin tope, confiando solo en el límite por minuto.
4. **Coincidencia por nombre fuera de la zona al final de la lista.** La ficha dice «aparece ese experto si está activo» sin condicionar la zona; se muestra, pero marcado `atiendeTuColonia = false`. Alternativa: ocultarlo, que contradice el CA4 de la ficha.
5. **Los términos sin resultado se guardan agregados por día y con TTL de 90 días.** La ficha pide guardarlos «sin usuario»; el filtro de la spec 024 evita que un teléfono escrito en la búsqueda acabe en la base. Alternativa: no guardarlos.
6. **El diccionario se edita por script y PR.** No hay panel interno con permisos todavía (HU-066); un cambio revisado es más seguro. Alternativa: ruta de administración, que exige roles que no existen.
7. **Ajustes al contrato propuesto por frontend 021:** se conserva la ruta y los parámetros, pero `oficioDetectado` pasa a `interpretacion` (para distinguir detectado, ambiguo, no entendido y elegido), `oficio` del elemento pasa a `oficios[]` (un técnico puede tener varios) y `colonia` pasa a `coloniaBase`. El 503 se conserva. Hay que reflejarlo en frontend 021.
8. **422 para formato y para catálogo inválido**, igual que las specs 020 a 026 (convención de la spec 023, decisión 8); 400 solo para el cursor, como en la 020.

## 15. Riesgos y dudas abiertas
- **Hueco: la colección `tecnicos` no tiene spec dueña.** Las specs 020, 021, 027 y 028 la leen y nadie define su alta, edición ni sus índices completos (HU-017 «perfil del técnico» no tiene spec). Para la demo se llena con semillas (KNOCK-246); hay que decidir quién la especifica.
- **`palabrasBusqueda` se calcula al escribir el técnico:** mientras no exista la spec de la ficha del técnico, la semilla debe calcularla con el mismo normalizador; si difiere, la búsqueda por nombre falla.
- **Colonias sin `centro`:** el catálogo de Tula tiene 8 de 12 colonias con polígono; las demás solo ven técnicos con la misma colonia base.
- **El diccionario inicial es corto:** la calidad depende de los términos sembrados; el registro anónimo de términos sin resultado sirve para ampliarlo.
- **Damerau-Levenshtein con distancia 2 en palabras largas puede confundir oficios:** se mide con los términos reales y, si hay falsos positivos, se baja a 1.
- **`$geoNear` en Azure DocumentDB:** se comprueba en el despliegue igual que la búsqueda por punto de TT-API-07.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-11
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
