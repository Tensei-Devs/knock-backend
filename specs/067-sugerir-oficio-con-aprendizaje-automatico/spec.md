# Spec 067 — Sugerir el oficio a partir de la descripción del problema, con aprendizaje automático

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** KNOCK-266 (relacionada con KNOCK-77 y KNOCK-108, que **no** quedan cumplidas con esta spec) · **Sprint:** 5 · **Responsable:** por asignar
> **Otra parte en:** knock-frontend, spec 051 «Sugerencia de oficio en la búsqueda»
> **Fuentes:** requisitos del profesor (citados en §1) · `docs/DECISION_TECNICA.md` (IA en el producto: Azure OpenAI y ML.NET) · ADR-008 · specs 021, 027, 045, 046 y 052 · `docs/descripcion-del-proyecto.md` §11.2 · TT-ML-01 y HU-060
> **Skills:** `ai-features`, `vertical-slice-api`, `security-core`, `testing-strategy-backend`

## 1. Objetivo
Cumplir con la parte de aprendizaje automático de lo que pide el profesor con la opción más pequeña que se pueda construir, medir y demostrar sin presentar reglas como si fueran un modelo.

**Texto escrito del profesor** (documento de requisitos, punto 16, citado tal cual):
> «-Al menos deberá de tener una función de ML y  IA generativa dentro del proyecto Móvil»

**Texto escrito del profesor** (punto 19, citado tal cual):
> «19.- Proyectos ML (Machine Learning  e IA Generativa) Para los proyectos ML e IA Generativa que se requieran en su proyecto 3 Se podrán usar las funciones que ofrecen la nube que seleccionaron. Sí no se tiene esos servicios deberán de crearlos. La creación de los proyectos ML e IA Generativa deberán desarrollarse usando las mismas prácticas que backend y móvil»

El texto escrito no nombra un algoritmo, un servicio ni una herramienta, y no pide que el modelo se entrene con datos reales.

**Qué se propone:** un clasificador entrenado por el equipo con ML.NET que, a partir de una frase que describe el problema, sugiere hasta tres oficios del catálogo. La persona decide.

**Qué no es:** el orden «Recomendadas» sigue siendo una regla fija (spec 021, R-05) y así se debe nombrar en cualquier pantalla, documento o demostración. No es aprendizaje automático.

## 2. Historia y alcance
**2.1 Historia.** Como persona que no sabe qué oficio necesita, quiero describir mi problema y recibir una sugerencia, para elegir más rápido.

**2.2 Dentro**
- Datos de entrenamiento **sintéticos**, rotulados como tales, versionados en el repositorio.
- Un conjunto de evaluación escrito a mano por personas del equipo, aparte de esos datos.
- Un programa de entrenamiento reproducible, el modelo versionado y un reporte de métricas.
- `POST /api/v1/asistente/sugerencia-oficio`, detrás de un interruptor apagado por omisión.

**2.3 Fuera**
- Cambiar «Recomendadas», las oleadas, el rango de precio o la detección por sinónimos de la spec 027.
- Reentrenamiento automático o programado, y aprender de lo que escriben las personas.
- IA generativa (spec 052, Azure OpenAI), imágenes y voz.
- Decidir algo sin la persona: no crea solicitudes ni filtra técnicos por sí solo.

## 3. Requisitos
- **R-01:** **Datos de entrenamiento.** Archivo de texto versionado con columnas `texto`, `oficioClave` y `origen = sintetico`, y una ficha que diga cómo se generaron, con qué herramienta, quién los revisó y sus límites. Todos los oficios incluidos tienen la misma cantidad de ejemplos, con 40 como mínimo cada uno. No contienen datos de personas reales ni textos tomados de Knock en uso.
- **R-02:** **Conjunto de evaluación independiente.** Como mínimo 10 frases por oficio incluido, escritas a mano, sin IA ni plantillas, por personas del equipo que no hayan leído los datos de entrenamiento antes de escribirlas. Ninguna frase se repite, ya normalizada, entre los dos conjuntos. Se registra quién escribió cada una. No se usa para entrenar ni para ajustar el modelo; si se usa para ajustar, deja de ser independiente y el reporte lo dice.
- **R-03:** **Entrenamiento reproducible.** Clasificación de texto de varias clases con ML.NET, con semilla fija y versión exacta del paquete. El programa produce el modelo, su huella SHA-256 y el reporte. Dos corridas con la misma entrada dan las mismas predicciones sobre el conjunto de evaluación.
- **R-04:** **Reporte honesto.** Incluye, sobre el conjunto de evaluación: exactitud, F1 macro, matriz de confusión, proporción que queda sin sugerencia y los mismos datos para **dos líneas base**: elegir siempre el mismo oficio y la detección por palabras y sinónimos de la spec 027. Si el modelo no supera a la detección por sinónimos, el reporte lo dice con esas palabras y la decisión de mostrarlo se revisa.
- **R-05:** **Ruta.** Exige JWT; cuerpo `{ texto }` de 8 a 500 caracteres después de recortar, y cuerpo de 4 KB como máximo.
- **R-06:** Responde `200 { estado, sugerencias: [{ oficioClave, nombre, puntaje }], modelo: { version, datosDeEntrenamiento: "sinteticos" } }`. `estado` es `sugerido` o `sin_confianza`; con `sin_confianza` la lista va vacía. Devuelve como máximo 3 oficios, solo activos en el catálogo, de mayor a menor `puntaje`. `puntaje` es la salida del modelo entre 0 y 1; no es una probabilidad de acierto.
- **R-07:** El umbral de `sin_confianza` es configuración (`SugerenciaOficio:Umbral`). El valor se fija con una separación de los datos de entrenamiento, nunca con el conjunto de evaluación.
- **R-08:** **Interruptor.** `SugerenciaOficio:Enabled` es `false` por omisión. Apagado, o sin modelo, o con huella distinta de la configurada, la ruta responde `503 suggestion_unavailable` y el resto del servicio funciona igual. No hay respaldo con reglas bajo esta ruta.
- **R-09:** **Privacidad.** El texto se procesa en el propio servicio de Mercado, no sale a ningún servicio externo, no se guarda, no se cachea y no se registra. Los registros llevan versión del modelo, estado, cantidad de sugerencias y latencia.
- **R-10:** Límite de 20 peticiones por minuto por persona, con 429 y `Retry-After`. `Cache-Control: private, no-store`.
- **R-11:** **Recursos.** Antes de encenderla en un despliegue se miden, en el contenedor de Mercado con sus límites configurados, la memoria en reposo y bajo carga, el tiempo de arranque y el percentil 95 de la ruta. Metas: p95 menor a 300 ms con el modelo cargado y ningún reinicio por memoria. Si no se cumplen, no se enciende y se anota.
- **R-12:** **Dependencia.** `Microsoft.ML` (licencia MIT según NuGet; versión estable consultada el 2026-11-28: 6.0.0). Antes de agregarla se confirma que compila y corre con el .NET del repositorio, se revisan sus dependencias transitivas, licencias y vulnerabilidades, y se registra en la memoria.
- **R-13:** **Cómo se nombra.** Toda documentación y respuesta dice que es un modelo entrenado con datos sintéticos y que sus métricas no demuestran cómo se comporta con usuarios reales.

## 4. Criterios de aceptación
- **CA-01:** Dado el archivo de entrenamiento, cuando se valida, entonces todas las filas tienen `origen = sintetico`, un oficio del catálogo, la misma cantidad por oficio (40 como mínimo) y ningún teléfono, correo ni CURP.
- **CA-02:** Dados los dos conjuntos, cuando se comparan ya normalizados, entonces no comparten ninguna frase y el de evaluación tiene al menos 10 por oficio con su autoría registrada.
- **CA-03:** Dada la misma entrada y semilla, cuando se entrena dos veces, entonces las predicciones sobre el conjunto de evaluación son iguales.
- **CA-04:** Dado el reporte, cuando se genera, entonces trae exactitud, F1 macro, matriz de confusión, proporción sin sugerencia y las dos líneas base calculadas sobre el mismo conjunto.
- **CA-05:** Dada la función encendida y un texto válido, cuando se llama, entonces responde 200 con la forma de R-06, como máximo 3 oficios activos y ordenados por puntaje.
- **CA-06:** Dado un texto que queda por debajo del umbral, entonces `sin_confianza` y lista vacía.
- **CA-07:** Dado un oficio desactivado en el catálogo, cuando el modelo lo prefiere, entonces no aparece.
- **CA-08:** Dada la función apagada, sin modelo o con huella distinta, entonces 503 `suggestion_unavailable` y las demás rutas de Mercado responden igual.
- **CA-09:** Dada una petición sin token, con texto fuera de rango, cuerpo ilegible, cuerpo mayor a 4 KB o por encima del límite, entonces responde 401, 422 (`errors.texto`), 400, 413 o 429 sin repetir el texto.
- **CA-10:** Dadas las respuestas de error, registros, trazas, caché y base, cuando se inspeccionan, entonces el texto no aparece y no sale ninguna llamada externa para clasificarlo.
- **CA-11:** Dado el contrato publicado, entonces la ruta declara sus respuestas y la puerta de entrada la enruta con sesión.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 a CA-04 | `UnitTest/Assistant/Spec067EntrenamientoTests` |
| CA-05 a CA-08, CA-11 | `IntegrationTest/Assistant/Spec067SugerenciaTests` y `ContractTest/Spec067ContractTests` |
| CA-09, CA-10 | `SecurityTest/Assistant/Spec067SeguridadTests` |

## 5. Contrato
| Ruta | Quién | Entrada | Éxito | Errores |
|---|---|---|---|---|
| `POST /api/v1/asistente/sugerencia-oficio` | persona con sesión | `{ texto }` | `200` (R-06) | `400`, `401`, `413`, `422`, `429`, `503 suggestion_unavailable` |

La puerta de entrada declara las rutas del asistente una por una: se agrega la entrada de esta.

## 6. Modelo de datos
Sin colecciones nuevas. Archivos versionados: datos sintéticos, conjunto de evaluación, ficha, reporte y modelo con su huella.

## 7. Arquitectura y componentes
Programa de entrenamiento aparte (no se publica en la imagen de Mercado). En Mercado, módulo `Assistant`, un puerto `ISugeridorDeOficio` con una implementación de ML.NET que carga el modelo una vez y admite peticiones simultáneas de forma segura.

## 8. Dependencias
Specs 002, 009, 011, 027 (línea base y catálogo), 045 y 052 (módulo y puerta de entrada). Para la demostración en el teléfono: frontend 051 y un despliegue autorizado de Mercado.

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| Presentar reglas o datos inventados como evidencia real | Rótulo de datos sintéticos, líneas base y límites escritos | CA-01, CA-04 |
| Métrica inflada por evaluar con lo mismo que se entrenó | Conjunto independiente sin frases compartidas | CA-02 |
| El texto de la persona se filtra o se guarda | Se procesa en memoria y no se registra | CA-10 |
| Cambio del archivo del modelo | Huella comprobada al cargar | CA-08 |
| Abuso que consume CPU | Sesión, tamaño y límite | CA-09 |
| La sugerencia se toma como decisión | Solo sugiere; la persona elige | CA-05, CA-06 |

## 10. Plan de pruebas
Unitarias de datos, entrenamiento y reporte; integración de la ruta con un modelo de prueba pequeño y con el real; seguridad negativa; contrato; medición de recursos de R-11 por comando.

## 11. Escenarios
**Positivo:** «sale agua por debajo del lavabo» sugiere plomería. **Negativo:** «quiero un pastel» queda `sin_confianza` o devuelve una sugerencia equivocada: ambos resultados se anotan como se vean. **Error esperado:** función apagada (503). **Límite:** texto de 8 y de 500 caracteres. Los ejemplos ilustran el uso; no son resultados medidos.

## 12. Definición de terminado
CA-01 a CA-11 con prueba, `python scripts/sdd_lint.py --trace 067` en OK, ficha y reporte publicados, mediciones de R-11 anotadas, OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` al día, gates de `AGENTS.md §5` y memoria. Para decir que el requisito del profesor está demostrado hace falta además verlo funcionar en el teléfono contra el entorno desplegado.

## 13. Decisiones pendientes de quien aprueba
- **D-01 (la principal):** qué función de aprendizaje automático se construye. Esfuerzos sin medir.

| Opción | Esfuerzo (backend y app) | A favor | En contra |
|---|---|---|---|
| **A. Sugerir el oficio desde el texto (esta spec, propuesta)** | 11 a 18 h | Entra texto y sale una clase: se puede medir con frases que el modelo no vio y compararlo con los sinónimos; se demuestra escribiendo en el teléfono | Datos sintéticos; se parece a la búsqueda de la 027 y a lo que hará el asistente generativo; no es lo que dice hoy la descripción del proyecto |
| B. «Recomendadas» con ML.NET (HU-060) | Sin estimar con detalle; mayor que A (idea inicial: 18 a 30 h) | Es lo que dice la descripción del proyecto | No hay historial real: con datos sintéticos de cumplimiento el modelo aprendería la fórmula con la que se inventaron, y no habría con qué comprobar que predice algo |
| C. Rango de precio por regresión | Parecido a B | También está en la descripción del proyecto | El mismo problema que B; hoy la referencia usa percentiles (spec 048), que no son un modelo |
| D. Servicio de IA preentrenado de Azure | Sin estimar; depende del servicio | El punto 19 menciona las funciones de la nube | No se ha comprobado cuota, costo ni región. Si es la moderación de contenido, ya responde a otro requisito (punto 16) y podría contarse dos veces |

- **D-02:** ¿El profesor acepta un modelo propio con ML.NET, o espera un servicio de Azure cuando la nube lo ofrece? El punto 19 dice «Se podrán usar las funciones que ofrecen la nube» y «Sí no se tiene esos servicios deberán de crearlos»: no es claro. *Propuesta: preguntarlo antes de construir.*
- **D-03:** ¿Qué oficios entran? *Propuesta: todos los activos del catálogo si cada uno puede cumplir R-01 y R-02; si no, un subconjunto escrito en la ficha.*
- **D-04:** ¿El modelo se guarda en el repositorio o se entrena al construir la imagen? *Propuesta: guardarlo con su huella, si su tamaño es razonable.*
- **D-05:** Si se aprueba, la descripción del proyecto debe decir qué está construido y que HU-060 sigue pendiente.

## 14. Decisiones tomadas sin consulta
1. **En Mercado, módulo Asistente**, sin servicio nuevo ni recurso de Azure nuevo.
2. **Sin respaldo por reglas** en la ruta, para que nunca se confunda una regla con el modelo.
3. **ML en el servidor** y no en el teléfono: no agrega dependencias a la app ni depende de servicios de Google del teléfono.

## 15. Riesgos y limitaciones
- Con datos sintéticos las métricas pueden verse mejor de lo que serían con personas reales. El conjunto independiente reduce ese riesgo, pero es pequeño y lo escribe el mismo equipo.
- Frases con errores, modismos o dos problemas a la vez pueden fallar.
- La memoria de ML.NET en el tamaño actual del contenedor no está medida.
- El profesor podría no aceptar esta función como suficiente (D-02).
- **Estimación sin medir** del backend: 8 a 13 horas (datos y conjunto de evaluación, 2 a 4; entrenamiento y reporte, 2 a 3; ruta y pruebas, 3 a 4; recursos y documentación, 1 a 2). La app suma 3 a 5. No incluye CI, revisión ni despliegue.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
