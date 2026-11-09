# KNOCK-78 · Riesgo aceptado: acceso de red público de MongoDB

Excepción registrada: **EXC-017** en `security/exceptions.yaml`. Fecha: 2026-11-06. Responsable: José Julián Hernández Hernández (Tech Lead).

## 1. Situación verificada (lectura de Azure, 2026-11-06)

- El clúster `knock-docdb` (Azure DocumentDB, nivel gratuito, versión 8.0) tiene el acceso de red público **habilitado** y ninguna conexión de punto de conexión privado.
- Tiene tres reglas de firewall, creadas a mano y fuera de Terraform (`deploy/database/` solo importa el clúster):
  - `azure-services`, el rango especial 0.0.0.0: lo usan las Container Apps para llegar a la base.
  - Dos reglas de IP individual de administración: una de Alexis y una del líder (`mi-ip-julian`).
- El grupo de recursos `rg-knock-dev` tiene un candado `CanNotDelete` llamado `no-borrar`.
- Cada una de las cinco Container Apps expone 161 direcciones de salida posibles, por lo que no se pueden declarar una por una en el firewall.

## 2. Motivo de la aceptación

Cerrar el acceso público exige red privada o Private Link, que tienen costo. El proyecto opera a costo cero (`docs/evidencia/KNOCK-145-costo-cero.md`, que no incluye puntos de conexión privados). Reemplazar `azure-services` por las direcciones de salida de las apps no es viable por su cantidad.

## 3. Controles compensatorios

- Toda conexión exige usuario y contraseña, y el servicio impone TLS en todas las conexiones (Microsoft Learn, «Secure your cluster - Azure DocumentDB», consultado el 2026-11-06).
- Una cadena de conexión distinta por servicio (`mongo-identity`, `mongo-market`, `mongo-messaging`), guardada solo en Key Vault; nunca en el repositorio ni en Terraform (`deploy/README.md`).
- Acceso a Key Vault por RBAC con un permiso por secreto y por servicio (EXC-012).
- El clúster está protegido contra borrado por `prevent_destroy` y por el candado `no-borrar`.
- Solo existen la regla `azure-services` y reglas de IP individual; no hay rangos amplios.

## 4. Revisión y condición de salida

- Fecha de revisión: **2026-12-05**, fin de la ventana de evaluación. Se revisa la lista de reglas y se retiran (inutilizan) las IP individuales que ya no se usen.
- La excepción vence el 2027-02-04 (máximo de 90 días).
- Condición de salida: cerrar el acceso público cuando el proyecto tenga red privada o Private Link sin costo adicional, o pase a una suscripción de pago. Si no ocurre, se renueva con una nueva revisión.

## 5. Análisis: retirar la IP individual del líder sin perder acceso y sin costo

Solo lectura; no se ejecutó ningún cambio.

### Quién usa qué hoy

| Quién | Regla | Observación |
|---|---|---|
| Las cinco Container Apps | `azure-services` (0.0.0.0) | Indispensable: sin ella los servicios pierden la base. No se toca. |
| Alexis | regla de IP individual propia | Sigue vigente hasta que él la libere. |
| Líder | `mi-ip-julian` | La que se quiere retirar. |
| Guillermo y Erick | ninguna | No tienen regla propia hoy. |

### Efecto del candado

Un candado `CanNotDelete` en el grupo de recursos bloquea las operaciones DELETE sobre el grupo y sus recursos hijos, incluidas las reglas de firewall; **no bloquea PUT**. Por eso una regla **no se puede borrar**, pero sí **sobrescribir**: se le cambia el rango por una dirección que no enruta, con lo que deja de dar acceso. La alternativa de quitar el candado un momento, borrar la regla y volver a crearlo no cuesta, pero deja el grupo sin protección durante ese lapso y requiere permiso sobre `Microsoft.Authorization/locks`; se descarta mientras la sobrescritura baste.

### Alternativas gratuitas

1. **Neutralizar la regla (recomendada).** Sobrescribir `mi-ip-julian` con la dirección de documentación 192.0.2.1 (RFC 5737, no enruta en Internet). Es una operación PUT, permitida por el candado, y deja de existir acceso desde la IP del líder. No se usa 0.0.0.0, porque ese valor es la regla especial de servicios de Azure.
2. **Regla temporal mientras se trabaja.** Dejar la regla neutralizada y, solo cuando alguien necesite entrar, sobrescribirla con su IP actual y devolverla al valor neutro al terminar. Es el mismo recurso, sin costo, y evita que una IP doméstica quede abierta por semanas; además la IP de casa cambia, así que la regla permanente envejece mal.
3. **Script de abrir y cerrar.** Un script de `az` (con `az rest` sobre `firewallRules`) que lee la IP pública actual, sobrescribe la regla de la persona y, con un `trap`, la devuelve al valor neutro al salir. Debe vivir bajo `deploy/` o `scripts/` (CODEOWNERS: solo el Tech Lead) y no imprimir la IP ni la cadena de conexión. No se creó en este PR; queda como decisión del líder.
4. **No viable sin costo:** Private Link, red privada o NAT con IP fija.

### Procedimiento que ejecutaría el líder

1. Confirmar sesión y suscripción: `az account show` (suscripción del proyecto).
2. Listar las reglas actuales y anotar nombre y rango de `mi-ip-julian`: `az rest --method get --url ".../providers/Microsoft.DocumentDB/mongoClusters/knock-docdb/firewallRules?api-version=2024-07-01"`.
3. Verificar que un servicio sigue conectando por `azure-services`: abrir `/health/ready` del gateway desplegado antes del cambio (debe responder 200).
4. Sobrescribir la regla con un PUT al mismo recurso `firewallRules/mi-ip-julian`, con `startIpAddress` y `endIpAddress` iguales a 192.0.2.1.
5. Volver a listar las reglas y comprobar que `mi-ip-julian` cambió, que `azure-services` y la de Alexis siguen igual y que `/health/ready` sigue en 200.
6. Probar que desde la máquina del líder ya no se conecta con `mongosh`; es la prueba de que el acceso se retiró.
7. Cuando haga falta entrar: repetir el paso 4 con la IP actual del líder y, al terminar, volver al valor neutro. Avisar al equipo del cambio.
8. Si el PUT fuera rechazado por el candado, no forzar nada: registrar el mensaje y decidir si se retira el candado de forma temporal.

Para Alexis, Guillermo y Erick aplica lo mismo: cada persona usa una regla propia, que se abre solo mientras trabaja.
