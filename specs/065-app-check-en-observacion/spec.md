# Spec 065 — Comprobación de app legítima (Firebase App Check) en modo de observación

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** KNOCK-266 · **Sprint:** 5 · **Responsable:** por asignar
> **Otra parte en:** knock-frontend, spec 050 «App Check en la app, en modo de observación»
> **Fuentes:** ADR-004 (Firebase solo para Auth y FCM; este borrador propone sumar App Check) · specs 001, 009 y 011 · [verificar tokens de App Check en un backend propio](https://firebase.google.com/docs/app-check/custom-resource-backend) (consultado el 2026-11-28)
> **Skills:** `security-core`, `auth-firebase-jwt`, `testing-strategy-backend`

## 1. Objetivo
Medir qué parte de las peticiones llega con una prueba válida de que salieron de la app de Knock (token de Firebase App Check, que en Android se emite con Play Integrity), **sin rechazar ni cambiar ninguna petición**. Con esa medición una persona decide después, en otra spec o en una enmienda firmada, si se exige y en qué rutas.

## 2. Historia y alcance
**2.1 Historia.** Como equipo de Knock, quiero saber cuántas peticiones vienen de la app legítima antes de exigirlo, para no dejar fuera a personas con teléfonos que no pueden demostrarlo.

**2.2 Dentro**
- Verificar en la puerta de entrada el encabezado `X-Firebase-AppCheck` de las rutas que usa la app.
- Clasificar cada petición y registrar solo el resultado agregable (métrica y registro estructurado).
- Interruptor `AppCheck:Mode` con dos valores: `off` (por omisión) y `observe`.
- Quitar el encabezado antes de reenviar la petición al servicio interno.

**2.3 Fuera**
- **Exigir** el token (modo `enforce`): no existe en esta spec; un valor distinto de `off` u `observe` impide arrancar.
- Activar «Aplicar» (*enforcement*) en la consola de Firebase para cualquier producto, en especial Authentication.
- Protección contra repetición (consumir el token), rutas del panel, `/health`, rutas internas y el canal `/hubs/chat`.
- La app (frontend 050) y el aviso de privacidad (landing).

## 3. Requisitos
- **R-01:** `AppCheck:Mode` es `off` por omisión. Con `off` no hay verificación, llamadas a Google, métricas ni cambios de comportamiento. Con un valor desconocido, o con `observe` sin `AppCheck:ProjectNumber` y sin al menos un identificador en `AppCheck:AllowedAppIds`, la puerta de entrada no arranca y el error no imprime valores.
- **R-02:** Con `observe`, **ninguna** petición cambia de estado, cuerpo ni encabezados de respuesta por el resultado de la comprobación.
- **R-03:** La comprobación aplica a las rutas con la política CORS de la app (`knock`). No aplica a las del panel, `/health`, `/health/ready`, las rutas internas, `/hubs/chat` ni las peticiones `OPTIONS`.
- **R-04:** El token se valida como indica la documentación de Firebase: firma con las llaves de `https://firebaseappcheck.googleapis.com/v1/jwks`, `alg` `RS256` (cualquier otro se rechaza), `typ` `JWT`, `iss` `https://firebaseappcheck.googleapis.com/<número de proyecto>`, `aud` que incluya `projects/<número de proyecto>`, `exp` vigente y `sub` (identificador de la app) en `AllowedAppIds`. A la fecha de la consulta, la documentación ofrece la verificación del Admin SDK para Node.js, Python, Go y Java, no para .NET: se usan las bibliotecas JWT que la puerta de entrada ya tiene, sin dependencia nueva.
- **R-05:** Cada petición evaluada tiene exactamente un resultado: `valido`, `ausente`, `invalido` (forma, firma o reclamos), `vencido`, `app_no_permitida` o `sin_verificador` (no hay llaves disponibles).
- **R-06:** La petición nunca espera una descarga de llaves: se obtienen y renuevan en segundo plano, con caché y un tiempo límite de 5 segundos. Un `kid` desconocido pide una renovación, como máximo una cada 5 minutos. Sin llaves, el resultado es `sin_verificador` y la petición sigue.
- **R-07:** Una excepción dentro de la comprobación no llega a la petición: se cuenta como `sin_verificador`.
- **R-08:** Se publica un contador `knock.appcheck.requests` con las etiquetas `resultado` y plantilla de la ruta, y como máximo un registro estructurado por resultado y ruta cada minuto. Ni la métrica ni el registro llevan el token, sus reclamos, el `sub` de la persona, la dirección IP, el identificador de dispositivo ni la ruta con valores.
- **R-09:** El encabezado `X-Firebase-AppCheck` no se reenvía a los servicios internos ni aparece en registros o trazas. Un encabezado de más de 4 KB se cuenta como `invalido` sin intentar leerlo.
- **R-10:** App Check no sustituye ninguna autenticación ni autorización: un token válido no da acceso a nada.
- **R-11:** El número de proyecto y los identificadores de app llegan por configuración de despliegue. La spec no agrega secretos.

## 4. Criterios de aceptación
- **CA-01:** Dado `Mode=off`, cuando llegan peticiones con y sin encabezado, entonces las respuestas son las de siempre, no hay métrica y no sale ninguna llamada a Google.
- **CA-02:** Dado `Mode=observe`, cuando llega una petición sin token, con un texto que no es un token, vencido, firmado con otra llave, con `alg` distinto de `RS256`, de otro proyecto o de una app no permitida, entonces la respuesta es igual a la de `off` y se cuenta el resultado de R-05 que corresponde.
- **CA-03:** Dado un token de prueba firmado con una llave de prueba publicada por un servidor de llaves simulado, cuando llega, entonces se cuenta `valido`.
- **CA-04:** Dado el servidor de llaves caído o lento, cuando llegan peticiones, entonces ninguna espera la descarga, se cuentan `sin_verificador` y las respuestas no cambian.
- **CA-05:** Dado un lote de peticiones con `kid` desconocidos en menos de 5 minutos, entonces hay como máximo una renovación de llaves.
- **CA-06:** Dada una petición con el encabezado, cuando se reenvía, entonces el servicio interno no lo recibe; y los registros, trazas y métricas no contienen el token.
- **CA-07:** Dadas peticiones del panel, de salud, `OPTIONS` y del canal de chat, entonces no se evalúan ni se cuentan.
- **CA-08:** Dada una configuración con `Mode=enforce`, o con `observe` sin número de proyecto o sin apps permitidas, cuando arranca la puerta de entrada, entonces falla sin imprimir valores.
- **CA-09:** Dado un token `valido` en una ruta que exige sesión, cuando llega sin JWT de Knock, entonces responde 401.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01, CA-02, CA-07 | `IntegrationTest/Gateway/Spec065ObservacionTests` |
| CA-03, CA-05 | `UnitTest/Gateway/Spec065VerificadorTests` |
| CA-04, CA-06, CA-08, CA-09 | `SecurityTest/Gateway/Spec065SeguridadTests` |

## 5. Contrato
No cambia ninguna ruta ni respuesta. Se admite el encabezado opcional `X-Firebase-AppCheck`.

## 6. Modelo de datos
Sin colecciones ni índices. No se guarda ningún token.

## 7. Arquitectura y componentes
Un middleware en `Knock.Gateway/Hosting` (la puerta de entrada, spec 001) y un servicio en segundo plano para las llaves. Los servicios internos no cambian.

## 8. Dependencias y requisitos previos
- Specs 001, 009 y 011.
- Registrar la app Android en App Check en la consola de Firebase (frontend 050). Lo hace el líder.
- Para ver resultados reales en dev hacen falta el despliegue y la variable `AppCheck__Mode=observe` (`deploy/**`, con autorización expresa).

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| La observación bloquea a personas legítimas | No hay camino de rechazo; solo `off` y `observe` | CA-02, CA-08 |
| Token falsificado contado como válido | Validación completa, algoritmo fijo y lista de apps | CA-02, CA-03 |
| Caída o lentitud de Google afecta a Knock | Llaves en segundo plano, sin espera | CA-04 |
| Provocar descargas repetidas de llaves | Una renovación cada 5 minutos como máximo | CA-05 |
| El token queda en registros | No se registra ni se reenvía | CA-06 |
| Tomar App Check como autenticación | La autorización no cambia | CA-09 |

## 10. Plan de pruebas
Unitarias del verificador con llaves de prueba, integración de la puerta de entrada con un servidor de llaves simulado y seguridad negativa. Ninguna prueba del CI llama a Google.

## 11. Escenarios
**Positivo:** la app con App Check envía el token y se cuenta `valido`. **Negativo:** un cliente sin token recibe la respuesta normal y se cuenta `ausente`. **Límite:** las llaves aún no han llegado al arrancar (`sin_verificador`).

## 12. Definición de terminado
CA-01 a CA-09 con prueba, `python scripts/sdd_lint.py --trace 065` en OK, configuración documentada, gates de `AGENTS.md §5` y memoria. La medición en dev se anota aparte, con sus cifras y su periodo. Tener el código con `off` no cuenta como observación activa.

## 13. Decisiones pendientes de quien aprueba
- **D-01:** ¿Se suma App Check a lo que ADR-004 permite usar de Firebase? Si es así, se enmienda el ADR. *Propuesta: sí, solo en observación.*
- **D-02:** ¿Se despliega `observe` en dev antes de la evaluación o queda el código con `off`? *Propuesta: encenderlo en dev dentro de un despliegue ya autorizado; no hacer uno solo por esto.*
- **D-03:** ¿Con qué datos se podrá proponer exigirlo? *Propuesta mínima: 14 días de medición, proporción de `valido` anotada con la versión de la app publicada y revisión de quiénes quedarían fuera. Exigirlo requiere otra firma.*

## 14. Decisiones tomadas sin consulta
1. **En la puerta de entrada** y no en cada servicio.
2. **Sin dependencia nueva** en el backend.
3. **Sin lista de rutas sensibles:** se observa lo que usa la app y se decide con datos.

## 15. Riesgos y dudas abiertas
- El teléfono de pruebas usa microG. Los veredictos altos de Play Integrity piden un dispositivo certificado, así que ese teléfono puede salir como `ausente`. No está probado. En observación no le afecta.
- Un token válido dificulta el abuso desde clientes falsos; no demuestra quién es la persona ni que el teléfono sea seguro.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
