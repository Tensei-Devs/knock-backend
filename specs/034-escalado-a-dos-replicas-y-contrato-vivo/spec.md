# Spec 034 — Escalado a dos réplicas y contrato vivo

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo:** KNOCK-76 (TT-CD-02) y KNOCK-137 (TT-CI-02) · **Otra parte en:** knock-frontend (spec 006, contrato vendorizado)
> **Servicio:** los tres servicios y el gateway; infraestructura (`deploy/`) y flujos de CI (`.github/workflows/`) · **Colecciones:** ninguna
> **Sprint:** propuesto Sprint 5 (insumo §3 y calendario: «contrato vivo, dos réplicas» del 17 al 20 de noviembre); Jira las etiqueta `sprint-4` · **Responsable sugerido:** José Julián (asignado en Jira a las dos)

## 1. Objetivo
Dejar el entorno de evaluación (25 de noviembre a 5 de diciembre) corriendo con dos réplicas fijas de cada servicio de negocio y del gateway, desplegable sin cortar el servicio y con una reversión ensayada; y que el contrato OpenAPI que consume el frontend salga siempre del código, se compruebe en cada PR contra las respuestas reales y llegue solo al repositorio de frontend cuando cambia. Esta spec **no repite** lo que ya fijan la 006 (rutas de salud, encabezados reenviados, estado en Redis) y la 007 (checks del PR, imagen, escaneos): los usa y completa lo que les falta para la evaluación.

## 2. Historias y alcance
**2.1 Historias**
- **TT-CD-02 (KNOCK-76)** — Producción con 2 réplicas fijas en la ventana de evaluación, balanceo de Container Apps y reversión probada.
- **TT-CI-02 (KNOCK-137)** — Contrato vivo: exportación del OpenAPI, `docs/openapi*.json`, publicación del contrato y pruebas de contrato.

**2.2 Dentro**
- Perfil de réplicas del entorno de evaluación y su prueba de Terraform.
- Sondas de arranque, disponibilidad y vida con valores fijados, y apagado ordenado de cada réplica.
- Despliegue sin corte bajo carga, comprobación del reparto entre réplicas y aprobación manual del despliegue.
- Reversión a la imagen anterior con un ensayo documentado.
- Contrato por servicio y contrato público combinado (lo que expone el gateway).
- Pruebas de contrato contra las respuestas reales.
- Control de cambios incompatibles con una etiqueta explícita.
- Flujo `contract-publish.yml` que abre o actualiza el PR del contrato en knock-frontend.

**2.3 Fuera**
- Azure Front Door y WAF: la ficha de TT-CD-02 los pide, pero cuestan sin nivel gratuito (ADR-006 y ADR-007); quedan fuera por el costo cero.
- Autoescalado (spec 006, decisión «escalado y no autoescalado»).
- Rutas de salud, encabezados reenviados y estado compartido (spec 006); las redes de confianza de `ForwardedHeaders` (spec 035).
- Los checks del PR que ya existen (compilación, formato, pruebas, Semgrep, Gitleaks, OSV, Trivy; spec 007).
- La automatización de CA-01 y CA-02 de la spec 006 (KNOCK-201): aquí solo se ejecutan contra el entorno real como parte del humo.
- Lo que hace el repositorio de frontend al recibir el PR (regenerar sus clientes; su spec 006).
- Un Bicep o un grupo de recursos aparte para producción: la infraestructura es Terraform (spec 005) y hay un solo entorno desplegado (spec 007).

## 3. Requisitos
**Réplicas y salud**
- **R-01:** El perfil `evaluacion` de `deploy/apps` fija `min = max = 2` para `gateway`, `identity`, `messaging` y `market`, y Redis sigue en 1 réplica. El perfil `dev` deja 1 en todos. Una prueba de Terraform (`terraform test` en `deploy/tests/`) carga los dos perfiles y falla si algún valor difiere; ningún perfil define reglas de escalado.
- **R-02:** Las sondas de cada servicio y del gateway quedan así: arranque en `/health` cada 5 segundos hasta 10 fallos; disponibilidad en `/health/ready` cada 10 segundos, 3 segundos de espera y 3 fallos para salir de rotación; vida en `/health` cada 10 segundos y 3 fallos para reiniciar. La sonda de vida **nunca** apunta a `/health/ready`, para que una caída de MongoDB o Redis saque a la réplica de rotación sin reiniciarla en bucle.
- **R-03:** Al recibir la señal de apagado, cada réplica marca `/health/ready` en 503 de inmediato, deja de aceptar conexiones nuevas, termina las peticiones en curso durante un máximo de 25 segundos (`HostOptions.ShutdownTimeout`) y cierra las conexiones del canal de tiempo real para que los clientes reconecten (spec 026). El periodo de gracia del contenedor es de 30 segundos.
- **R-04:** El despliegue usa el modo de revisión única: la revisión nueva recibe tráfico solo cuando sus réplicas pasan la sonda de disponibilidad, y la anterior se apaga con el apagado ordenado de R-03.
- **R-05:** Con dos réplicas y una carga de 20 usuarios virtuales durante un despliegue completo de los cuatro componentes, la tasa de respuestas 5xx y de errores de conexión es menor a 0.5 % y la latencia p95 menor a 1 segundo (prueba k6 de la spec 008; las 429 no cuentan como error).
- **R-06:** El reparto se comprueba sin exponer la réplica: el humo envía 200 peticiones por el gateway con `X-Correlation-Id` conocidos y una consulta de Application Insights cuenta las instancias (`cloud_RoleInstance`) distintas por componente; deben ser 2 en cada uno. Ninguna respuesta incluye el nombre de la réplica ni del contenedor.

**Despliegue y reversión**
- **R-07:** El despliegue al entorno de evaluación exige una acción humana: el flujo `ci-deploy` usa un entorno de GitHub `evaluacion` con revisión obligatoria si el plan del repositorio lo permite; si no lo permite, ese paso solo corre con `workflow_dispatch` lanzado por una persona con permiso de escritura. Ningún despliegue a evaluación ocurre solo por un `push`.
- **R-08:** La reversión vuelve a desplegar, con el mismo flujo y por Terraform, la etiqueta de imagen anterior (SHA completo del commit) anotada en `docs/VERSIONES_DESPLEGADAS.md`; solo se aceptan imágenes ya publicadas y escaneadas en el registro. `ci-deploy` acepta la entrada `image_tag` en `workflow_dispatch`, validada como 40 caracteres hexadecimales.
- **R-09:** La reversión se ensaya al menos una vez antes del 24 de noviembre: desplegar una versión, revertir a la anterior y correr el humo. Se mide el tiempo desde la decisión hasta el humo en verde (meta: menos de 15 minutos) y la evidencia (fecha, versiones, duración, resultado del humo; sin direcciones ni identificadores de la suscripción) se agrega a `docs/evidencia/`.
- **R-10:** El cambio a dos réplicas se aplica el 24 de noviembre y el regreso a una el 6 de diciembre, con los pasos manuales de la spec 005 documentados en `deploy/README.md`. El consumo adicional sale del crédito de estudiante (no es un servicio nuevo); antes de escalar, una persona revisa la alerta de presupuesto del ADR-006 y anota la estimación en `Memoria.md`.
- **R-11:** No se crean recursos de Front Door, WAF ni CDN; el único punto público sigue siendo la entrada del gateway (spec 001). Una regla del análisis de Terraform falla si aparece uno.

**Contrato vivo**
- **R-12:** Cada servicio con controllers REST exporta su contrato con `scripts/export_openapi.py`: Mercado en `docs/openapi.v1.json` (el nombre se conserva), Identidad en `docs/openapi.identity.v1.json` y Mensajería en `docs/openapi.messaging.v1.json` cuando tenga controllers REST (el canal de tiempo real queda fuera del OpenAPI, spec 026). Nunca se edita un contrato a mano.
- **R-13:** El script genera además el **contrato público** `docs/openapi.publico.v1.json`: la unión de los contratos de servicio limitada a las rutas que el gateway expone, con orden estable de rutas y esquemas, sin `servers` y con `info.title = "Knock API"`. Si dos servicios definen un esquema con el mismo nombre y distinto contenido, o la misma ruta y verbo, la generación falla con un mensaje que nombra el conflicto.
- **R-14:** El contrato declara en `ProblemDetails` los campos `code`, `correlationId` y `traceId`, y en cada respuesta el encabezado `X-Correlation-Id`; en 429 y 503, `Retry-After`. Lo exige una prueba sobre el contrato generado.
- **R-15:** En cada PR, el CI regenera los contratos de servicio y el público y falla si difieren de los del repositorio (ya existe para dos archivos; se extiende a todos). `oasdiff` compara el contrato público de la rama destino con el del PR y falla ante un cambio incompatible, salvo que el PR tenga la etiqueta `contrato-incompatible`; con ella, el check pasa y publica en el resumen del PR la lista de cambios incompatibles.
- **R-16:** `tests/ContractTest` levanta cada servicio con sus dependencias en contenedor y, por cada operación del contrato de ese servicio, ejecuta al menos una petición por cada código documentado que se pueda provocar sin servicios externos reales, y valida contra el esquema el cuerpo, el tipo de contenido y los encabezados declarados. Además falla si el servicio expone una ruta que no está en su contrato o el contrato declara una ruta que el servicio no atiende.
- **R-17:** `contract-publish.yml` corre en `push` a `develop` y a `main` cuando cambia algún `docs/openapi*.json` y por `workflow_dispatch`. Copia el contrato público a la rama `contrato/actualizacion` de knock-frontend como `contracts/openapi.v1.json` y abre un PR hacia la rama de integración del frontend o actualiza el que ya esté abierto; el cuerpo del PR lleva el SHA del commit de origen y el resumen de `oasdiff`. Si el contrato no cambió respecto al del frontend, no hace nada.
- **R-18:** La credencial de R-17 es un token de corta vida de una GitHub App instalada **solo** en knock-frontend con permisos `contents: write` y `pull_requests: write`; su llave privada vive en los secretos del entorno `contrato` del backend, al que solo acceden ejecuciones de `develop` y `main`. El flujo no corre en PR ni en ramas de bifurcaciones, declara `permissions: contents: read` a nivel de flujo, fija cada acción por SHA y nunca imprime el token.
- **R-19:** Antes de publicar, el flujo comprueba que el contrato público no tenga `servers`, nombres de host internos (`knock-identity`, `knock-market`, `knock-messaging`, `knock-redis`, `.internal.`), ni ejemplos con datos personales (celulares con `+52`, CURP, correos); si encuentra alguno, falla sin publicar.

## 4. Criterios de aceptación
- **CA-01:** Dado el perfil `evaluacion`, cuando corre `terraform test`, entonces `gateway`, `identity`, `messaging` y `market` tienen `min = max = 2`, Redis tiene 1 y no hay reglas de escalado; con el perfil `dev`, todos tienen 1.
- **CA-02:** Dada la configuración de las sondas en Terraform, cuando corre `terraform test`, entonces la de vida apunta a `/health`, la de disponibilidad a `/health/ready` y los intervalos y umbrales son los de R-02.
- **CA-03:** Dada una réplica que recibe la señal de apagado con una petición de 5 segundos en curso, cuando se apaga, entonces `/health/ready` responde 503 de inmediato y la petición en curso termina con 200.
- **CA-04:** Dado MongoDB inaccesible para un servicio, cuando pasan 60 segundos, entonces `/health` sigue en 200, `/health/ready` en 503 y el contenedor no se reinicia.
- **CA-05:** Dado el entorno de evaluación con dos réplicas, cuando corre el humo de 200 peticiones por el gateway, entonces la consulta de Application Insights muestra 2 instancias distintas por componente, ninguna respuesta 5xx y ninguna respuesta con el nombre de una réplica.
- **CA-06:** Dada una carga k6 de 20 usuarios virtuales, cuando se despliega una revisión nueva de los cuatro componentes, entonces los errores 5xx y de conexión son menos de 0.5 % y la p95 es menor a 1 segundo.
- **CA-07:** Dado un `push` a la rama principal, cuando corre `ci-deploy`, entonces el paso de despliegue a evaluación espera la aprobación de una persona o no corre sin `workflow_dispatch`.
- **CA-08:** Dado `workflow_dispatch` con un `image_tag` que no son 40 hexadecimales o que no existe en el registro, cuando corre, entonces falla antes de tocar Azure.
- **CA-09:** Dado el ensayo de reversión, cuando se revierte a la versión anterior, entonces el humo pasa, la duración es menor a 15 minutos y la evidencia queda en `docs/evidencia/` sin datos de la suscripción.
- **CA-10:** Dado el código de Terraform, cuando corre el análisis de infraestructura, entonces no existe ningún recurso de Front Door, WAF ni CDN y solo el gateway tiene entrada externa.
- **CA-11:** Dado un PR que cambia un endpoint sin regenerar los contratos, cuando corre el CI, entonces el check del contrato falla y nombra el archivo.
- **CA-12:** Dado un PR que quita un campo obligatorio de una respuesta, cuando corre el CI sin la etiqueta `contrato-incompatible`, entonces falla; con la etiqueta, pasa y el resumen del PR lista el cambio.
- **CA-13:** Dado un controller nuevo sin su operación en el contrato, o una respuesta que no coincide con su esquema, cuando corre `tests/ContractTest`, entonces falla y nombra la ruta.
- **CA-14:** Dados dos contratos de servicio con un esquema del mismo nombre y distinto contenido, cuando se genera el contrato público, entonces falla y nombra el esquema; sin conflicto, el contrato público contiene las rutas de Identidad y Mercado que expone el gateway y ninguna interna.
- **CA-15:** Dado el contrato público, cuando se inspecciona, entonces `ProblemDetails` declara `code`, `correlationId` y `traceId`, cada respuesta declara `X-Correlation-Id`, y 429 y 503 declaran `Retry-After`.
- **CA-16:** Dado un `push` a `develop` que cambia el contrato, cuando corre `contract-publish.yml`, entonces existe un único PR abierto en knock-frontend desde `contrato/actualizacion` con el contrato nuevo en `contracts/openapi.v1.json`; un segundo `push` lo actualiza en lugar de abrir otro; un `push` sin cambios no lo toca.
- **CA-17:** Dado un PR o una rama de una bifurcación, cuando se revisa la definición de `contract-publish.yml`, entonces el flujo no corre en esos eventos, sus permisos son los mínimos de R-18, sus acciones están fijadas por SHA y ningún paso imprime el token.
- **CA-18:** Dado un contrato público con `servers`, un host interno o un ejemplo con un celular `+52`, cuando corre la comprobación previa a publicar, entonces falla y no se abre ningún PR.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | (comando) `terraform test` en `deploy/` con `replicas_evaluacion.tftest.hcl` |
| CA-02 | (comando) `terraform test` en `deploy/` con `sondas.tftest.hcl` |
| CA-03 | `Spec034ApagadoTests.Drena_Y_Sale_De_Rotacion_CA_034_03` |
| CA-04 | `Spec034SaludTests.Vida_No_Depende_De_Mongo_CA_034_04` |
| CA-05 | (comando) humo de dos réplicas y consulta de Application Insights; salida pegada en el PR |
| CA-06 | (comando) `k6 run tests/PerformanceTest/k6/despliegue-sin-corte.js` durante un despliegue |
| CA-07 | (comando) revisión de `ci-deploy.yml` con `actionlint` y ejecución real documentada |
| CA-08 | (comando) `workflow_dispatch` con etiquetas inválidas en el entorno de evaluación |
| CA-09 | (comando) ensayo de reversión con evidencia en `docs/evidencia/` |
| CA-10 | (comando) `checkov -d deploy` con la regla de recursos prohibidos |
| CA-11 | `test_export_openapi.py::test_contrato_desactualizado_falla_CA_034_11` |
| CA-12 | (comando) PR de prueba con un cambio incompatible, con y sin etiqueta |
| CA-13 | `Spec034ContratoTests.Rutas_Y_Esquemas_Coinciden_CA_034_13` |
| CA-14 | `test_export_openapi.py::test_contrato_publico_conflictos_CA_034_14` |
| CA-15 | `Spec034ContratoTests.Errores_Y_Encabezados_Declarados_CA_034_15` |
| CA-16 | (comando) ejecución de `contract-publish.yml` en `develop` documentada en el PR |
| CA-17 | (comando) `actionlint` y la revisión de gobierno de flujos del repositorio |
| CA-18 | `test_export_openapi.py::test_publicacion_rechaza_datos_internos_CA_034_18` |

## 5. Contrato
Esta spec no agrega endpoints de negocio. Fija estas interfaces:

| Interfaz | Forma |
|---|---|
| Variable de Terraform `replicas` | mapa `{ gateway, identity, messaging, market } → { min, max }`; perfiles `dev` (1) y `evaluacion` (2) |
| Sondas | arranque `/health` (5 s, 10 fallos); disponibilidad `/health/ready` (10 s, espera 3 s, 3 fallos); vida `/health` (10 s, 3 fallos); gracia de 30 s |
| `ci-deploy.yml` | entrada `workflow_dispatch.image_tag` (40 hexadecimales, opcional); entorno de GitHub `evaluacion` |
| Archivos de contrato | `docs/openapi.v1.json` (Mercado), `docs/openapi.identity.v1.json`, `docs/openapi.messaging.v1.json` (cuando aplique), `docs/openapi.publico.v1.json` (combinado) |
| Etiqueta de PR | `contrato-incompatible` |
| `contract-publish.yml` | disparadores `push` (`develop`, `main`, rutas `docs/openapi*.json`) y `workflow_dispatch`; entorno `contrato`; rama destino `contrato/actualizacion` en knock-frontend; archivo `contracts/openapi.v1.json` |
| Contrato publicado | sin `servers`; `ProblemDetails` con `type`, `title`, `status`, `detail`, `code`, `correlationId`, `traceId`; `X-Correlation-Id` en toda respuesta; `Retry-After` en 429 y 503 |

**Códigos y errores:** no hay códigos nuevos; los de cada servicio se declaran en el contrato tal como los fija su spec.

**Límites:** 2 réplicas fijas en evaluación; 25 segundos de drenado; 0.5 % de errores y p95 de 1 segundo durante un despliegue; 15 minutos para revertir; un PR abierto de contrato a la vez.

## 6. Modelo de datos
Sin colecciones ni llaves nuevas. Los artefactos son archivos:
- `docs/openapi.publico.v1.json` (generado) y, cuando aplique, `docs/openapi.messaging.v1.json`.
- `deploy/tests/replicas_evaluacion.tftest.hcl` y `deploy/tests/sondas.tftest.hcl`.
- `tests/PerformanceTest/k6/despliegue-sin-corte.js`.
- `docs/evidencia/KNOCK-76-reversion.md` (evidencia del ensayo) y la consulta de Application Insights del humo en `docs/OBSERVABILIDAD.md`.
- El estado de las réplicas y revisiones vive en Azure y en Terraform; no hay estado de negocio nuevo.

## 7. Arquitectura y componentes
- **Terraform:** `deploy/apps/variables.tf` conserva `replicas`; los perfiles fijan los valores. `deploy/modules/service` agrega el periodo de gracia, los valores de la sonda de vida y lo que falte de R-02; sigue en `revision_mode = "Single"`.
- **Servicios y gateway:** `Hosting/` agrega el apagado ordenado: un indicador de «apagándose» que la comprobación de disponibilidad consulta, `HostOptions.ShutdownTimeout = 25 s` y el cierre del canal de tiempo real en Mensajería. Se aplica en los cuatro proyectos con la duplicación ya registrada para `Hosting/` (EXC-007 y EXC-015) o, si para entonces existe, en la biblioteca técnica común.
- **CI:** `ci-deploy.yml` agrega `workflow_dispatch` con `image_tag`, la validación de la etiqueta contra el registro y el entorno `evaluacion`. `ci-pr.yml` extiende el paso de contrato a todos los archivos y el de `oasdiff` al contrato público con la etiqueta de excepción. Archivo nuevo `.github/workflows/contract-publish.yml`.
- **Script:** `scripts/export_openapi.py` agrega `--service messaging` y `--publico` (unión, filtro por las rutas de `ReverseProxy` del gateway, conflictos) y la comprobación previa a publicar; sus pruebas en `scripts/test_export_openapi.py`.
- **Pruebas de contrato:** `tests/ContractTest` con una fábrica por servicio (reutiliza las de integración), un lector del contrato y un validador de esquemas JSON.
- Estos archivos están protegidos para los agentes (`.github/`, `scripts/`, `docs/openapi*.json`): los cambia una persona con revisión, como fija `AGENTS.md`.

## 8. Dependencias
- **Base:** specs 001 (gateway como único punto público), 002 (contrato por servicio), 005 (Terraform y pasos manuales), **006** (salud, encabezados reenviados, estado en Redis; esta spec no los redefine), **007** (CI, imagen, escaneos), 008 (k6 y calidad), 011 (Application Insights y correlación), 012 (versión y `docs/VERSIONES_DESPLEGADAS.md`), 026 (reconexión del canal de tiempo real) y 035 (redes de confianza para que el límite por IP funcione con dos réplicas).
- **ADR-006** (costos y ventana de evaluación) y **ADR-007** (sin CDN).
- **Frontend:** spec 006 de knock-frontend vendoriza `contracts/openapi.v1.json`; con esta spec, ese archivo pasa a ser el contrato **público combinado** (hoy se copia el de Mercado). Debe confirmarlo el equipo de frontend.
- **Herramientas y servicios:** GitHub App propia (sin costo), `actions/create-github-app-token` fijada por SHA, `oasdiff` (ya en uso), k6 (spec 008), validador de esquemas JSON para .NET por evaluar y registrar en `Memoria.md` (por ejemplo `JsonSchema.Net`, licencia MIT).

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Una réplica enferma recibe tráfico | Sonda de disponibilidad con 3 fallos y salida de rotación | CA-02, CA-04 |
| Reinicios en bucle por una dependencia caída | La sonda de vida no depende de MongoDB ni de Redis | CA-04 |
| Peticiones cortadas al desplegar o apagar | Drenado de 25 segundos y revisión nueva solo cuando está lista | CA-03, CA-06 |
| Despliegue accidental o malicioso a evaluación | Entorno con aprobación o disparo manual | CA-07 |
| Revertir a una imagen sin escanear o inventada | `image_tag` validado contra el registro, solo imágenes publicadas por el CI | CA-08 |
| Sobrecosto por réplicas olvidadas | Fechas de subida y bajada, perfil `dev` por omisión y alerta de presupuesto | CA-01 |
| Recursos de pago creados por error (Front Door, WAF) | Regla del análisis de Terraform | CA-10 |
| Revelar la topología interna (réplicas, hosts) | Sin nombre de réplica en respuestas; contrato público sin `servers` ni hosts internos | CA-05, CA-18 |
| El frontend consume un contrato que no coincide con el servidor | Contrato generado del código, pruebas de contrato contra respuestas reales y regeneración obligatoria | CA-11, CA-13 |
| Cambios incompatibles silenciosos | `oasdiff` bloquea salvo etiqueta explícita y visible | CA-12 |
| Robo de la credencial que escribe en el repositorio de frontend | GitHub App instalada solo en ese repositorio, permisos mínimos, token de corta vida, entorno restringido a `develop` y `main`, sin ejecución en PR ni bifurcaciones | CA-17 |
| Acción de terceros comprometida | Acciones fijadas por SHA (spec 007) | CA-17 |
| Datos personales en ejemplos del contrato | Comprobación previa a publicar | CA-18 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Indicador de apagado y comprobación de disponibilidad; funciones del script (unión, conflictos, filtro de rutas, comprobación de datos internos) |
| Integración | Sí | Host de prueba que recibe la señal de apagado con una petición en curso; servicio con MongoDB detenido |
| Contrato | Sí | `tests/ContractTest` por servicio y prueba de que el contrato declara errores y encabezados |
| Seguridad negativa | Sí | `image_tag` inválido, contrato con hosts internos o datos personales, flujo de publicación en eventos no permitidos |
| Regresión | Sí | Todas las suites existentes deben seguir en verde con el apagado ordenado y el contrato extendido; el cliente generado del frontend se regenera sin errores con el contrato público |
| Infraestructura | Sí | `terraform validate`, `terraform test` de perfiles y sondas, Checkov con la regla de recursos prohibidos |
| CI/CD | Sí | `actionlint` sobre los flujos modificados y nuevo; ejecución real de `contract-publish.yml` en `develop`; ensayo de despliegue y reversión |
| Análisis estático y vulnerabilidades | Sí | Semgrep y Gitleaks sobre los flujos y el script; OSV-Scanner para el validador de esquemas nuevo |
| Mutación | No aplica | Scripts de despliegue y configuración; los casos negativos del script de unión de contratos (conflictos y datos internos) están en Unitarias |
| Rendimiento | Sí | k6 con 20 usuarios virtuales durante un despliegue (CA-06) |
| Humo en Azure | Sí | 200 peticiones por el gateway, reparto por instancia y CA-01 y CA-02 de la spec 006 contra el entorno real |
| Arquitectura | No | No cambia fronteras entre servicios |

## 11. Escenarios
**Positivos**
- El 24 de noviembre se aplica el perfil `evaluacion`: aparecen dos réplicas por componente y el humo las ve a las dos.
- Se despliega una corrección el 27 de noviembre con la carga de la evaluación en curso y nadie nota el cambio.
- Un PR agrega un campo opcional a una respuesta: el contrato se regenera, `oasdiff` no lo marca y, al fusionar en `develop`, el frontend recibe un PR con el contrato nuevo.
- Se revierte una versión defectuosa en menos de 15 minutos.

**Negativos**
- Alguien edita a mano `docs/openapi.v1.json`.
- Un PR renombra un campo sin la etiqueta `contrato-incompatible`.
- Una persona intenta desplegar una etiqueta que no está en el registro.
- Un PR desde una bifurcación intenta ejecutar la publicación del contrato.

**Errores esperados**
- Fallo del check de contrato desactualizado; fallo de `oasdiff` sin etiqueta; fallo de `ContractTest` con la ruta; fallo de la generación del contrato público por conflicto; fallo de `ci-deploy` por `image_tag` inválido; fallo de la publicación por datos internos.

**Casos límite**
- Una réplica tarda más de 50 segundos en arrancar (10 fallos de 5 segundos): Container Apps la reinicia y el despliegue no recibe tráfico hasta que esté lista.
- Una petición larga de 30 segundos durante el apagado: se corta a los 25 y el cliente reintenta (solo las operaciones idempotentes).
- Dos fusiones seguidas en `develop`: el segundo `publish` actualiza el mismo PR.
- El contrato de Mensajería todavía vacío: no se genera su archivo y el público no cambia.
- Un esquema con el mismo nombre e idéntico contenido en dos servicios: se fusiona sin error.

## 12. Definición de terminado
- CA-01 a CA-18 con prueba o comando en verde; las salidas de los comandos pegadas en el PR; `python scripts/sdd_lint.py --trace 034` sin faltantes salvo los marcados «(comando)».
- Ensayo de reversión documentado y humo con dos réplicas aprobado antes del 24 de noviembre.
- `contract-publish.yml` ejecutado al menos una vez con un PR real en knock-frontend; la GitHub App creada por el líder y su llave en el entorno `contrato`.
- `deploy/README.md` con los pasos de subida y bajada de réplicas y de reversión; `docs/OBSERVABILIDAD.md` con la consulta del reparto.
- `Memoria.md` con la decisión de no usar Front Door ni WAF, la GitHub App, el contrato público combinado y la estimación de consumo.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Dos réplicas fijas solo en la ventana:** es lo que pide la constitución (§5) y el ADR-006 al menor costo.
- **Revertir volviendo a desplegar la imagen anterior:** usa el mismo camino probado (Terraform y CI), evita diferencias entre lo declarado y lo desplegado y no necesita el modo de varias revisiones.
- **Contrato público combinado:** el frontend consume rutas de Identidad y de Mercado; un solo archivo evita que el cliente generado quede incompleto.
- **El backend empuja el contrato al frontend:** cumple el «Terminada cuando» de KNOCK-137 (abrir el PR del cliente en knock-frontend) con la credencial más acotada posible.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **Sin Front Door ni WAF**, aunque la ficha de TT-CD-02 y la descripción de KNOCK-76 los piden: no tienen nivel gratuito (ADR-006, ADR-007). Alternativa: Front Door Standard solo durante la ventana, pagado con el crédito de estudiante, si el líder lo autoriza por separado.
2. **Terraform y no Bicep**, aunque la ficha dice «Bicep»: la infraestructura del repositorio es Terraform (spec 005). Alternativa: ninguna razonable sin reescribir la infraestructura.
3. **Revertir por etiqueta de imagen y no activando la revisión anterior.** Con el modo de revisión única, activar una revisión vieja deja a Terraform desalineado con lo desplegado. Alternativa: modo de varias revisiones con reparto de tráfico, más flexible y más complejo.
4. **Aprobación por entorno de GitHub o, si el plan no lo permite, solo por disparo manual.** Los revisores obligatorios de entornos no están en todos los planes gratuitos para repositorios privados. Alternativa: aprobación fuera de GitHub, sin registro en el flujo.
5. **Reparto comprobado con Application Insights y no con un encabezado de réplica.** Exponer la réplica en cada respuesta revela la topología. Alternativa: el encabezado solo en el entorno de evaluación, que también lo verían los evaluadores.
6. **El contrato público reemplaza a `contracts/openapi.v1.json` en el frontend.** Hoy ese archivo es una copia del de Mercado y le faltan las rutas de Identidad. Alternativa: publicar varios archivos y que el frontend genere varios clientes.
7. **GitHub App en lugar de un token personal.** El token personal depende de una persona y suele tener más alcance. Alternativa: que el frontend jale el contrato con su propio flujo programado, que también necesita una credencial de lectura del backend si el repositorio es privado.
8. **Etiqueta `contrato-incompatible` para permitir cambios incompatibles.** Durante el Sprint 5 aún habrá cambios de contrato acordados; bloquearlos siempre frenaría al equipo. Alternativa: versionar la API (`/api/v2`) ante cualquier cambio incompatible, excesivo antes del 1.0.0.
9. **Umbrales del despliegue sin corte (0.5 % y p95 de 1 segundo con 20 usuarios).** Son valores de partida para la carga de la evaluación; se ajustan con la primera medición. Alternativa: cero errores, que no garantiza ningún balanceador al cambiar de revisión.
10. **Fechas de subida (24 de noviembre) y bajada (6 de diciembre).** Un día antes de la ventana para el humo y uno después para la entrega. Alternativa: dos réplicas desde el Sprint 5, que consume más crédito.

## 15. Riesgos y dudas abiertas
- **Pregunta 8 del insumo sin resolver:** si KNOCK-137 o KNOCK-76 son requisito antes del 25 de noviembre. Esta spec asume que entran en el Sprint 5 (17 al 20 de noviembre).
- **Crédito de estudiante:** ocho réplicas durante 12 días pueden superar el uso gratuito mensual de Container Apps; se paga con el crédito, pero hay que estimarlo antes de escalar.
- **Revisores obligatorios de entornos** pueden no estar disponibles en el plan del repositorio; entonces la protección es solo el disparo manual.
- **Coordinación con frontend:** el cambio de `contracts/openapi.v1.json` a contrato combinado regenera todo el cliente; debe hacerse en un PR revisado por frontend, no a mitad de una función.
- **Contrato de Mensajería:** si la spec 030 agrega endpoints REST, su exportación entra al contrato público; si no, Mensajería sigue sin archivo.
- **Las pruebas de contrato no cubren códigos que solo provocan servicios externos reales** (Firebase de producción, Azure OpenAI): se cubren con dobles y se anota cuáles.
- **Criterios pendientes de la spec 006:** su CA-01 y su CA-02 siguen sin prueba automática hasta que KNOCK-201 se cierre; el humo de esta spec las ejecuta a mano.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-11
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
