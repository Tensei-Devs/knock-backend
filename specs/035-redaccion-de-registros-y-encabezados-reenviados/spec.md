# Spec 035 — Redacción de registros con parámetros de consulta y encabezados reenviados

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo:** KNOCK-186 (TT-API-11 · Redactar los logs de petición con query) y KNOCK-180 (TT-AZ-06 · Fijar las redes conocidas de ForwardedHeaders en el despliegue)
> **Servicio:** gateway y los tres servicios (`Hosting/`); infraestructura (`deploy/apps`) · **Colecciones:** ninguna
> **Sprint:** KNOCK-186 en Sprint 4 (Jira e insumo); KNOCK-180 propuesta para Sprint 5 (insumo §3; Jira la etiqueta `sprint-4`) · **Responsables sugeridos:** Guillermo (KNOCK-186) y José Julián (KNOCK-180), como asigna Jira

## 1. Objetivo
Que ningún registro de Knock, a ningún nivel configurado, contenga los valores de los parámetros de consulta de una petición (coordenadas, texto de búsqueda, códigos postales, tokens), y que la IP del cliente que usan los límites de peticiones sea la real y no se pueda falsificar, con dos réplicas detrás de la entrada de Container Apps. Hoy, si alguien baja el nivel de `Microsoft.AspNetCore` a `Information`, los eventos «Request starting» y «Request finished» registran la consulta completa (por ejemplo `lat` y `lon`); y sin redes de confianza, todas las personas comparten la IP del proxy y 10 peticiones por minuto de una sola bloquearían `/auth` para todas.

## 2. Historias y alcance
**2.1 Historias**
- **TT-API-11 (KNOCK-186)** — Excluir o redactar los registros de `Microsoft.AspNetCore.Hosting.Diagnostics` sin importar el nivel, con una prueba que falle si alguien baja ese ajuste. *Terminada cuando:* ningún nivel configurado deja salir la consulta de una petición y hay una prueba que lo comprueba.
- **TT-AZ-06 (KNOCK-180)** — Fijar las redes conocidas de `ForwardedHeaders` por variable de entorno con el rango mínimo de la entrada de Container Apps. *Terminada cuando:* el despliegue no arranca sin la variable y el límite por IP distingue a dos clientes distintos detrás de la entrada.

**2.2 Dentro**
- Qué fuentes de registro se excluyen, qué se redacta y qué se conserva, en el gateway y en los tres servicios.
- La redacción de consultas de URL en cualquier texto de un registro, como última barrera.
- La prueba que arranca cada proyecto con los niveles más detallados y comprueba que no sale ninguna consulta.
- La validación al arrancar de las redes y proxies de confianza, y la regla de qué encabezados reenviados se aceptan y cuáles se reenvían.
- La prueba de que dos clientes detrás de la entrada caen en particiones distintas y de que un `X-Forwarded-For` falso no mueve a nadie de partición.

**2.3 Fuera**
- La telemetría de OpenTelemetry (trazas y métricas): ya quita `url.query` y redacta `url.full` (spec 011 y TT-OBS-01); aquí solo se agrega una prueba de regresión.
- El formato de los registros, la retención y las alertas (spec 011).
- Los valores de los límites de peticiones (spec 009 y `docs/rate-limit-matrix.md`).
- Un balanceador o Front Door delante de la entrada (spec 034, descartado por costo); si se agrega, esta spec se revisa (límite de saltos).
- Los registros de la app móvil y del panel (knock-frontend).

## 3. Requisitos
**Redacción de registros (KNOCK-186)**
- **R-01:** Los eventos de las fuentes `Microsoft.AspNetCore.Hosting.Diagnostics`, `Microsoft.AspNetCore.HttpLogging`, `System.Net.Http.HttpClient` (y sus subcategorías) y `Yarp.ReverseProxy.Forwarder` con nivel menor a `Warning` se descartan con un filtro de Serilog que se aplica **antes** de los niveles configurados; ningún ajuste de `appsettings` ni variable de entorno los puede habilitar. El registro de cada petición lo hace únicamente el registro propio de la spec 011 (`UseSerilogRequestLogging` con la plantilla de la ruta).
- **R-02:** El registro de cada petición conserva solo: método, plantilla de la ruta (`/api/v1/x/{id}`), código de respuesta, duración, `correlationId`, `traceId`, servicio y nivel. Nunca incluye la ruta con sus valores, la consulta, el host de origen, encabezados ni cuerpos.
- **R-03:** Las llamadas salientes (servicio a servicio, Firebase, Azure) se registran, si se registran, con el método, el nombre del cliente HTTP, el código y la duración; nunca con la dirección completa.
- **R-04:** Como última barrera, el formateador de registros (`RedactingJsonFormatter` con `TelemetryRedactor`) reemplaza por `[redacted]` todo lo que sigue a un `?` cuando ese `?` va después de una dirección o ruta reconocible (que empieza por `/`, `http://` o `https://`) que aparezca en el mensaje, en una propiedad de texto o en el texto de una excepción; un `?` de una frase en texto libre no se redacta (*decisión automática, revisar*) (`/ruta?a=1&b=2` → `/ruta?[redacted]`), conservando la parte anterior al `?`. La regla no tiene cuantificadores anidados y respeta el tiempo máximo de evaluación de las demás reglas.
- **R-05:** Los eventos de `Warning` o más de las fuentes de R-01 pasan por la redacción de R-04; un error de la puerta de entrada que incluya la dirección de destino no deja salir su consulta.
- **R-06:** El campo `instance` de los ProblemDetails es la ruta sin consulta.
- **R-07:** No se usa `UseHttpLogging` ni `AddHttpLogging` en ningún proyecto; una regla de Semgrep lo bloquea.
- **R-08:** Una prueba por proyecto (gateway, Identidad, Mensajería y Mercado) arranca el host con `Serilog:MinimumLevel:Default = Verbose` y todas las anulaciones de nivel en `Verbose`, captura los registros en un receptor en memoria, hace peticiones con consulta (`?lat=19.4326&lon=-99.1332&q=fuga%20de%20agua&cp=42000&token=abc`), una llamada saliente con consulta y, en el gateway, una petición reenviada con consulta, y falla si algún registro contiene un valor de la consulta, `lat=`, `lon=`, `q=`, `cp=` o `token=`. La prueba también falla si el filtro de R-01 no está registrado.

**Encabezados reenviados (KNOCK-180)**
- **R-09:** Fuera de `Development`, el gateway y los tres servicios **no arrancan** si `ForwardedHeaders:KnownNetworks` y `ForwardedHeaders:KnownProxies` están vacíos; el mensaje nombra la llave sin mostrar valores. En `Development` se conserva el comportamiento actual (sin confianza en nadie).
- **R-10:** Cada red de `KnownNetworks` debe ser privada (IPv4 de 10.0.0.0/8, 172.16.0.0/12 o 192.168.0.0/16; IPv6 de fc00::/7) y con un prefijo de al menos /16 en IPv4 y /48 en IPv6; una red pública, `0.0.0.0/0`, `::/0` o más amplia que eso impide arrancar.
- **R-11:** Terraform pasa `ForwardedHeaders__KnownNetworks__0` con la subred del entorno de Container Apps al gateway y a los tres servicios (hoy no la recibe Mensajería), y la variable `container_apps_subnet_cidr` valida el mismo rango de R-10. Nunca se usa `ASPNETCORE_FORWARDEDHEADERS_ENABLED`.
- **R-12:** Se procesan solo `X-Forwarded-For` y `X-Forwarded-Proto`, con `ForwardLimit = 1` (se toma el valor más a la derecha, el que agregó el proxy de confianza). `X-Forwarded-Host`, `X-Forwarded-Prefix` y `Forwarded` (RFC 7239) se ignoran.
- **R-13:** Al reenviar a un servicio, el gateway **reemplaza** `X-Forwarded-For` por un único valor, la IP del cliente que él resolvió, fija `X-Forwarded-Proto` al esquema resuelto y **quita** `X-Forwarded-Host`, `X-Forwarded-Prefix`, `Forwarded` y `X-Original-For` que vengan del cliente. Así un servicio nunca ve valores escritos por el cliente.
- **R-14:** La IP resuelta es la clave de partición de los límites por IP (spec 009) en el gateway y en los servicios, con la normalización vigente (IPv4 mapeada a IPv4 e IPv6 por /64). La IP no se escribe en registros (spec 011).
- **R-15:** La configuración de confianza se comprueba en el humo del entorno de evaluación: dos clientes con IP distinta detrás de la entrada caen en particiones distintas (uno puede llegar a 429 en `/auth` sin afectar al otro).

## 4. Criterios de aceptación
- **CA-01:** Dado cada proyecto arrancado con todos los niveles en `Verbose`, cuando recibe una petición con `?lat=19.4326&lon=-99.1332&q=fuga%20de%20agua&cp=42000&token=abc`, entonces ningún registro contiene esos valores ni los nombres `lat=`, `lon=`, `q=`, `cp=` o `token=`.
- **CA-02:** Dado el mismo arranque, cuando el servicio hace una llamada saliente con consulta, entonces ningún registro contiene la consulta ni la dirección completa.
- **CA-03:** Dado el gateway con todos los niveles en `Verbose`, cuando reenvía una petición con consulta, entonces ningún registro del gateway contiene la consulta.
- **CA-04:** Dado un proyecto sin el filtro de R-01 registrado (variante de prueba), cuando corre la prueba de CA-01, entonces falla.
- **CA-05:** Dado el registro de una petición, cuando se inspecciona, entonces tiene método, plantilla de la ruta, código, duración, `correlationId` y `traceId`, y no tiene la ruta con valores, el host ni encabezados.
- **CA-06:** Dados textos con `https://host/ruta?a=1`, `/api/v1/x?y=2#z`, una excepción cuyo mensaje incluye una dirección con consulta y un texto sin `?`, cuando pasan por la redacción, entonces la consulta queda como `?[redacted]`, la parte anterior se conserva y el texto sin `?` no cambia.
- **CA-07:** Dada una petición con consulta que termina en un error 500, cuando se inspeccionan el ProblemDetails y el registro, entonces `instance` no tiene consulta y el registro tampoco.
- **CA-08:** Dado un proyecto que llama a `UseHttpLogging`, cuando corre Semgrep, entonces el check falla.
- **CA-09:** Dado el entorno `Production` sin `KnownNetworks` ni `KnownProxies`, cuando arranca el gateway o cualquiera de los tres servicios, entonces el arranque falla con un mensaje que nombra la llave; en `Development` arranca.
- **CA-10:** Dadas las redes `0.0.0.0/0`, `8.8.8.0/24`, `10.0.0.0/8` (más amplia que /16) o una dirección mal escrita, cuando arranca un proyecto fuera de `Development`, entonces no arranca; con `10.20.0.0/23` arranca.
- **CA-11:** Dadas dos peticiones que llegan desde una IP de la red de confianza con `X-Forwarded-For` de dos clientes distintos, cuando se agota el límite de `/auth` del primero, entonces el primero recibe 429 y el segundo sigue recibiendo respuestas normales.
- **CA-12:** Dada una petición desde una IP fuera de la red de confianza con `X-Forwarded-For: 1.2.3.4`, cuando se resuelve la IP, entonces es la de la conexión y no `1.2.3.4`; dada una petición desde la red de confianza con `X-Forwarded-For: 1.2.3.4, 5.6.7.8`, la IP es `5.6.7.8`.
- **CA-13:** Dada una petición al gateway con `X-Forwarded-For`, `X-Forwarded-Host`, `Forwarded` y `X-Original-For` escritos por el cliente, cuando se reenvía, entonces el servicio recibe un solo `X-Forwarded-For` con la IP resuelta, `X-Forwarded-Proto` y ninguno de los otros tres.
- **CA-14:** Dado el módulo de Terraform, cuando se ejecuta `terraform test`, entonces el gateway y los tres servicios reciben `ForwardedHeaders__KnownNetworks__0` y la variable de la subred rechaza un rango público o más amplio que /16.
- **CA-15:** Dado el entorno de evaluación, cuando corre el humo de dos clientes, entonces uno llega a 429 en `/auth` y el otro no se ve afectado.
- **CA-16:** Dada una traza exportada de una petición con consulta, cuando se inspeccionan sus atributos, entonces `url.query` no existe y `url.full` no tiene valores de la consulta (regresión de la spec 011).

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec035RegistrosTests.Sin_Consulta_En_Ningun_Nivel_CA_035_01` |
| CA-02 | `Spec035RegistrosTests.Llamada_Saliente_Sin_Consulta_CA_035_02` |
| CA-03 | `Spec035GatewayRegistrosTests.Reenvio_Sin_Consulta_CA_035_03` |
| CA-04 | `Spec035RegistrosTests.La_Prueba_Falla_Sin_Filtro_CA_035_04` |
| CA-05 | `Spec035RegistrosTests.Registro_De_Peticion_Minimo_CA_035_05` |
| CA-06 | `Spec035RedaccionTests.Redacta_Consultas_En_Texto_CA_035_06` |
| CA-07 | `Spec035RegistrosTests.ProblemDetails_Sin_Consulta_CA_035_07` |
| CA-08 | (comando) `semgrep --config .semgrep` con un archivo de muestra que la regla debe marcar |
| CA-09 | `Spec035ConfianzaTests.No_Arranca_Sin_Redes_CA_035_09` |
| CA-10 | `Spec035ConfianzaTests.Rechaza_Redes_Amplias_O_Publicas_CA_035_10` |
| CA-11 | `Spec035LimitesTests.Dos_Clientes_Particiones_Distintas_CA_035_11` |
| CA-12 | `Spec035ConfianzaTests.Encabezado_Falso_No_Mueve_La_Ip_CA_035_12` |
| CA-13 | `Spec035GatewayTests.Reenvia_Solo_Encabezados_Propios_CA_035_13` |
| CA-14 | (comando) `terraform test` en `deploy/` con `encabezados_reenviados.tftest.hcl` |
| CA-15 | (comando) humo de dos clientes en el entorno de evaluación; salida pegada en el PR |
| CA-16 | `Spec035TelemetriaTests.Trazas_Sin_Consulta_CA_035_16` |

## 5. Contrato
No hay endpoints nuevos ni cambios en el OpenAPI. Interfaces que fija:

| Interfaz | Forma |
|---|---|
| Fuentes excluidas por debajo de `Warning` | `Microsoft.AspNetCore.Hosting.Diagnostics`, `Microsoft.AspNetCore.HttpLogging`, `System.Net.Http.HttpClient*`, `Yarp.ReverseProxy.Forwarder*` |
| Registro de petición | `RequestMethod`, `RequestPath` (plantilla), `StatusCode`, `Elapsed`, `correlationId`, `traceId`, `service`, `level` |
| Redacción de consultas | `…?<cualquier cosa>` → `…?[redacted]` hasta el siguiente espacio, comilla o `#` |
| Configuración | `ForwardedHeaders:KnownNetworks` (CIDR privadas, prefijo ≥ /16 o ≥ /48) y `ForwardedHeaders:KnownProxies` (IP); obligatorias fuera de `Development` |
| Encabezados aceptados del proxy de confianza | `X-Forwarded-For` (el último valor), `X-Forwarded-Proto` |
| Encabezados que el gateway reenvía a los servicios | `X-Forwarded-For` (un valor), `X-Forwarded-Proto`; quita `X-Forwarded-Host`, `X-Forwarded-Prefix`, `Forwarded`, `X-Original-For` |
| Variable de Terraform | `container_apps_subnet_cidr` con validación de R-10; `ForwardedHeaders__KnownNetworks__0` para los cuatro componentes |

**Códigos de error:** no hay; un fallo de configuración impide arrancar (la sonda de arranque lo detecta y el despliegue no recibe tráfico).

**Límites:** prefijo mínimo /16 (IPv4) y /48 (IPv6); `ForwardLimit = 1`; 100 ms máximo por regla de redacción (los de `TelemetryRedactor`).

## 6. Modelo de datos
Sin colecciones, llaves ni campos nuevos. Cambian la configuración de registro (`appsettings*.json` de los cuatro proyectos: se quitan anulaciones que ya no tienen efecto y se documenta el filtro), las opciones de `ForwardedHeaders` y las variables del módulo `deploy/apps`.

## 7. Arquitectura y componentes
- `Hosting/LoggingExtensions.cs` de cada proyecto agrega el filtro de fuentes (`Filter.ByExcluding`) antes de leer la configuración, de modo que ningún nivel lo anule; la lista de fuentes es una constante con prueba.
- `Hosting/TelemetryRedactor.cs` agrega la regla de consultas de URL (R-04) a la cadena de `Redact`, activada solo si el texto contiene `?`.
- Los ProblemDetails se crean con `instance` igual a `HttpContext.Request.Path` (sin `QueryString`).
- `Hosting/ForwardedHeadersExtensions.cs` (servicios) y `Hosting/CorsExtensions.cs` (gateway) validan al arrancar R-09 y R-10 con el entorno y fallan con un mensaje sin valores.
- El gateway agrega en YARP la transformación de encabezados reenviados de R-13 (`X-Forwarded` en modo reemplazar y eliminación de los demás) para todas las rutas.
- Mensajería registra `AddKnockForwardedHeaders()` y `UseForwardedHeaders()` como los otros servicios.
- `deploy/apps/main.tf` agrega `forwarded` a Mensajería; `variables.tf` valida la subred.
- Estos cambios tocan los `Hosting/` duplicados (EXC-007 y EXC-015): se aplican igual en los cuatro y las pruebas de la spec los recorren todos.
- Regla de Semgrep para `UseHttpLogging`/`AddHttpLogging` en la configuración del repositorio (la agrega una persona; `.github/` y reglas son archivos protegidos para agentes).

## 8. Dependencias
- **Base:** specs 006 (encabezados reenviados y confianza solo en el balanceador; esta spec fija cómo), 009 (límites por IP con Redis, R-04 y R-06), 011 (registro sin datos personales; esta spec cierra el hueco de la consulta) y 005 (variables de Terraform).
- **Relacionadas:** 023 (zona por ubicación: `lat` y `lon` viajan en la consulta), 027 (búsqueda por texto: `q` en la consulta, escrita en paralelo), 026 (`access_token` en la consulta del canal de tiempo real; el gateway ya la quita y esta spec agrega la barrera en los registros) y 034 (dos réplicas: el límite por IP necesita esta spec antes del humo).
- **Documentos:** `docs/OBSERVABILIDAD.md` y `docs/rate-limit-matrix.md` se actualizan con las fuentes excluidas y la regla de confianza.
- **Sin dependencias nuevas:** Serilog, YARP y el middleware de ASP.NET Core ya están en el repositorio.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Ubicación, texto de búsqueda o códigos postales en los registros | Filtro de fuentes antes de los niveles y redacción de consultas | CA-01, CA-02, CA-03 |
| Alguien baja el nivel de registro para depurar y filtra datos | El filtro no depende de la configuración y la prueba arranca con todo en `Verbose` | CA-01, CA-04 |
| Tokens en la consulta (`access_token`, `token`, `sig`) | Redacción de toda la consulta, no solo de llaves conocidas | CA-01, CA-06 |
| Dirección con consulta dentro de una excepción | Redacción en el texto de excepciones y `instance` sin consulta | CA-06, CA-07 |
| Registro HTTP completo activado por error | Regla de Semgrep contra `UseHttpLogging` | CA-08 |
| Desplegar sin redes de confianza (todos comparten la IP del proxy y se bloquean entre sí) | Arranque fallido fuera de `Development` | CA-09, CA-15 |
| Red de confianza demasiado amplia (cualquiera falsifica su IP) | Solo redes privadas con prefijo mínimo | CA-10, CA-14 |
| Falsificar `X-Forwarded-For` para evadir el límite o culpar a otra IP | `ForwardLimit = 1`, confianza solo en la subred, el gateway reemplaza el encabezado | CA-11, CA-12, CA-13 |
| `X-Forwarded-Host` o `Forwarded` para envenenar direcciones generadas | Se ignoran y el gateway los quita | CA-13 |
| Regresión en la telemetría | Prueba de trazas sin consulta | CA-16 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Regla de redacción de consultas (casos con y sin `?`, `#`, comillas, textos largos y tiempo máximo), validación de redes (privadas, prefijo, mal escritas) |
| Integración | Sí | Cada host con todos los niveles en `Verbose` y un receptor en memoria; gateway con un servicio simulado que devuelve los encabezados recibidos; límite por IP con Redis real |
| Seguridad negativa | Sí | Encabezados falsos, redes amplias o públicas, configuración vacía fuera de `Development` |
| Contrato | No | No cambia el OpenAPI; se comprueba que sigue igual |
| Rendimiento | No | La regla de redacción tiene prueba de tiempo máximo en unitarias; no hay carga que medir |
| Regresión | Sí | Pruebas de la spec 011 (registros sin datos personales) y de TT-OBS-01 (trazas sin consulta) en verde; las suites de límites de la spec 009 sin cambios |
| Arquitectura | Sí | Los cuatro proyectos registran el filtro y la validación (prueba que recorre los cuatro `Program`) |
| Mutación | Sí | Condiciones de la regla de redacción y de la validación de redes (meta de la spec 008) |
| Infraestructura | Sí | `terraform test` de la variable de la subred y de las variables de entorno de los cuatro componentes |
| CI/CD | Sí | Gates de la spec 007; la regla nueva de Semgrep corre en el PR |
| Análisis estático y vulnerabilidades | Sí | Semgrep (regla nueva), Gitleaks y CodeQL sin hallazgos nuevos |
| Humo en Azure | Sí | Dos clientes en el entorno de evaluación (CA-15) |

## 11. Escenarios
**Positivos**
- Una persona busca técnicos cerca con `lat` y `lon`: el registro dice `GET /api/v1/colonias/en-punto 200 35 ms` y nada más.
- Dos personas en redes distintas inician sesión a la vez: cada una tiene su propio contador.
- Un desarrollador sube el nivel a `Debug` en su máquina: sigue sin ver consultas.

**Negativos**
- Un atacante manda `X-Forwarded-For: 127.0.0.1` para evadir el límite.
- Un atacante manda `X-Forwarded-Host: sitio-malo.example` para envenenar direcciones.
- Alguien despliega con `ForwardedHeaders__KnownNetworks__0=0.0.0.0/0`.
- Alguien agrega `app.UseHttpLogging()` para depurar.

**Errores esperados**
- Arranque fallido por configuración de confianza ausente o insegura; check de Semgrep en rojo; prueba de registros en rojo si falta el filtro.

**Casos límite**
- Consulta vacía (`/ruta?`): queda `/ruta?[redacted]`.
- `?` dentro de un texto que no es una dirección (por ejemplo, una pregunta en un mensaje de error): se redacta lo que sigue hasta el siguiente espacio; se acepta la pérdida.
- IPv6 con zona o IPv4 mapeada en IPv6 en `X-Forwarded-For`.
- `X-Forwarded-For` con 50 valores: solo cuenta el último.
- Subred `/16` exacta (se acepta) y `/15` (se rechaza).
- Errores de YARP de nivel `Warning` con la dirección de destino: salen redactados.

## 12. Definición de terminado
- CA-01 a CA-16 con prueba o comando en verde; `python scripts/sdd_lint.py --trace 035` sin faltantes salvo los marcados «(comando)».
- Los cuatro proyectos con el filtro, la redacción y la validación de confianza; Mensajería con encabezados reenviados.
- Terraform con la variable validada y los cuatro componentes con `ForwardedHeaders__KnownNetworks__0`.
- `docs/OBSERVABILIDAD.md` y `docs/rate-limit-matrix.md` actualizados; `Memoria.md` con la decisión.
- Humo de dos clientes en el entorno de evaluación documentado en el PR; revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Excluir fuentes en lugar de confiar en el nivel:** el nivel lo puede cambiar cualquiera; el filtro vive en código y tiene prueba.
- **Redactar la consulta completa y no por llaves:** una lista de llaves siempre se queda corta (`q`, `cp`, futuros parámetros); la consulta no aporta nada útil a un registro.
- **El gateway reemplaza los encabezados reenviados:** los servicios nunca ven valores escritos por el cliente y la regla de confianza se aplica en un solo punto.
- **Fallar al arrancar sin redes de confianza:** un despliegue que arranca sin ellas se bloquea a sí mismo en `/auth` (lo advierte la matriz de límites); es mejor no arrancar.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **Las dos claves en una sola spec.** KNOCK-186 y KNOCK-180 tocan el mismo `Hosting/` de los cuatro proyectos y las mismas pruebas de registro y de límites; separarlas duplicaría la revisión. Alternativa: dos specs pequeñas.
2. **Se excluyen también `System.Net.Http.HttpClient` y `Yarp.ReverseProxy.Forwarder`**, aunque KNOCK-186 solo nombra `Hosting.Diagnostics`: ambas registran direcciones completas con consulta en `Information`. Alternativa: solo la fuente de la ficha.
3. **Por debajo de `Warning` se descarta; de `Warning` para arriba se redacta.** Los avisos y errores sirven para diagnosticar y conservan su valor sin la consulta. Alternativa: descartar todo de esas fuentes.
4. **Prefijo mínimo /16 y solo redes privadas.** La subred de Container Apps es privada y mucho más chica; /16 deja margen sin abrir la puerta a rangos públicos. Alternativa: aceptar solo el CIDR exacto de Terraform, que obliga a sincronizar dos valores.
5. **Mensajería también recibe la red de confianza**, aunque hoy no limita por IP: necesita `X-Forwarded-Proto` para HSTS y su canal en tiempo real pasa por el gateway. Alternativa: dejarla sin cambios hasta que limite por IP.
6. **La validación aplica en todo entorno salvo `Development`.** Las pruebas de integración corren en `Development` o con su configuración explícita. Alternativa: una bandera aparte, que es otra cosa por olvidar.

## 15. Riesgos y dudas abiertas
- **Si se agrega un balanceador delante de la entrada** (Front Door, un proxy), `ForwardLimit = 1` tomaría la IP del balanceador; esta spec se revisa en ese caso.
- **La subred del entorno debe contener las IP del proxy de entrada de Container Apps:** se confirma en el humo (CA-15); si no fuera así, hay que pasar el rango del proxy en `KnownNetworks`.
- **La redacción de `?` en texto libre puede quitar información útil** en mensajes que no son direcciones; se acepta.
- **Duplicación de `Hosting/`:** el mismo cambio en cuatro copias (EXC-007, EXC-015) es propenso a olvidos; la prueba que recorre los cuatro proyectos lo mitiga hasta que exista la biblioteca técnica común.
- **El borrador de la spec 027 (búsqueda por texto) se escribe en paralelo:** si define parámetros de consulta, quedan cubiertos por la redacción completa sin cambios aquí.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
