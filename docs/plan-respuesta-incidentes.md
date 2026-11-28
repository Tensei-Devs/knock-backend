# Plan de respuesta a incidentes de seguridad

Backend de Knock, proyecto académico en piloto. Dice qué hacer cuando algo parece un incidente: secreto expuesto, acceso no autorizado, fuga o alteración de datos, vulnerabilidad que alguien está explotando, abuso que dispara el costo o caída causada por un ataque. Reportar una vulnerabilidad se explica en [`SECURITY.md`](../SECURITY.md).

**Cómo leerlo:** solo describe lo que existe en el repositorio. Lo marcado como **Hueco** no existe, no está definido o no se ha comprobado. **Ningún procedimiento de este documento se ha ensayado como simulacro.** Las órdenes sobre Azure, Key Vault o GitHub las ejecuta una persona autorizada, nunca un agente de IA (`AGENTS.md`).

## 1. Detección

| Fuente | Qué avisa | Límites |
|---|---|---|
| Alerta de métricas de Azure Monitor sobre `requests/failed` (Terraform, `deploy/modules/shared`) | Pico de peticiones fallidas en 5 minutos, severidad 2 de Azure | Cuenta también respuestas 4xx, salvo 401 ([`OBSERVABILIDAD.md`](OBSERVABILIDAD.md)). Solo envía correo si se dio `alert_email_receiver`. **Hueco:** este documento no comprueba que esté aplicada ni que tenga destinatario |
| Alertas de registros de 5xx y de latencia | Consultas y umbrales en `OBSERVABILIDAD.md` | Se crean a mano. **Hueco:** sin evidencia en el repositorio de que existan |
| Alerta de presupuesto ([evidencia](evidencia/KNOCK-250-alerta-presupuesto.md)) | Gasto anormal, que puede indicar abuso | Solo avisa, no frena el gasto |
| Registros de las aplicaciones y Application Insights | Búsqueda por `correlationId` o `traceId` (consultas en `OBSERVABILIDAD.md`) | Application Insights solo recibe datos si el servicio tiene su cadena de conexión. Los registros salen redactados |
| Auditoría del panel (colección `panel_auditoria`) | Accesos y acciones del panel interno | Se consulta cuando ya hay una sospecha |
| GitHub secret scanning y push protection | Secreto de un proveedor reconocido en el repositorio, o intento de subirlo | Activados (comprobado el 2026-11-28). La búsqueda de patrones que no son de un proveedor y la comprobación de validez están apagadas |
| Actualizaciones de seguridad de Dependabot | Dependencias con vulnerabilidades conocidas | Activadas (comprobado el 2026-11-28) |
| CI (`ci-pr`: Gitleaks, Semgrep, OSV-Scanner, NuGet, Trivy, ZAP) | Hallazgos en cada PR y push a las ramas fijas | No tiene ejecución programada: sin cambios no vuelve a revisar |
| Una persona (equipo, profesorado o quien use el sistema) | Cualquier comportamiento extraño | **Hueco:** el reporte privado de vulnerabilidades de GitHub, que indica `SECURITY.md`, está desactivado (comprobado el 2026-11-28) y no hay otro canal privado definido para personas externas |

**Hueco:** no hay guardia ni vigilancia continua. Los fallos de la bandeja de salida y de las tareas programadas (`process_alert`) no tienen regla de alerta en Azure.

## 2. Roles

| Rol | Hace |
|---|---|
| Quien detecta | Avisa en privado al Tech Lead con la hora, lo que vio y dónde. No abre un issue público ni intenta reproducirlo contra el ambiente desplegado |
| Tech Lead (responsable del incidente) | Confirma, asigna la severidad, decide y ejecuta o autoriza la contención (administra Key Vault y los despliegues), comunica hacia fuera del equipo y cierra |
| Responsable del área afectada (según [`CODEOWNERS`](../.github/CODEOWNERS)) | Investiga, prepara la corrección con su prueba y comprueba el efecto |
| Registro | Anota la línea de tiempo: la persona que designe el Tech Lead; si no designa a nadie, el propio Tech Lead |

**Hueco:** no hay suplente del Tech Lead ni definición de quién puede ejecutar la contención si no está disponible.

## 3. Severidad

Ante la duda se toma el nivel más alto y se baja cuando haya evidencia. Un incidente con INE, selfie, CURP, dirección o teléfono se escala de inmediato al Tech Lead (`SECURITY.md`).

| Nivel | Cuándo | Respuesta |
|---|---|---|
| SEV-1 crítica | Exposición o acceso no autorizado, confirmado o probable, a esos datos sensibles; clave de firma, de cifrado o credencial del ambiente desplegado comprometida; explotación en curso | Contener antes de investigar a fondo. Se detiene todo despliegue que no sea la corrección |
| SEV-2 alta | Una cuenta o una cuenta del panel tomada; vulnerabilidad alta explotable sin evidencia de uso; servicio caído por un ataque | Contener en cuanto se confirme |
| SEV-3 media | Impacto limitado y sin datos expuestos: abuso frenado por el límite de peticiones, fallo de un control con otra barrera en pie | Corregir por el flujo normal con prioridad |
| SEV-4 baja | Sin impacto conocido: aviso de un escáner no explotable, intento fallido | Registrar y revisar |

Para corregir vulnerabilidades valen los plazos objetivo de `SECURITY.md`. **Hueco:** no hay plazos de contención comprometidos.

## 4. Contención

Antes de cambiar algo, si la espera no agrava el daño, guardar la evidencia de la sección 6.

### 4.1 Secreto expuesto
1. Revocarlo o rotarlo donde se emitió. Borrarlo del archivo o de la historia de Git no basta: el repositorio es público y se debe dar por copiado. No se fuerza ningún push.
2. Si es una clave de firma entre servicios (`interno-key-*`, `avisos-key-*`), usar [`rotacion-de-claves-de-servicio.md`](rotacion-de-claves-de-servicio.md). Ese procedimiento es para la rotación periódica y mantiene válida la clave anterior 24 horas. **Si la clave está comprometida no se configura `<Clave>:Previous` ni `PreviousUntil`**: sin la fecha, la anterior se rechaza. Las llamadas internas pueden fallar mientras se actualizan los servicios. Esta variante no se ha ensayado.
3. `field-encryption-key`: [`deploy/README.md`](../deploy/README.md) describe cómo agregar una versión nueva. La anterior se conserva para descifrar lo guardado, así que **no protege los datos ya cifrados** si la llave se filtró.
4. `search-cursor-key`: rotarla invalida los cursores de búsqueda (`deploy/README.md`).

**Huecos:**
- No hay procedimiento de rotación para `jwt-signing-key`, `panel-jwt-signing-key`, las cadenas de MongoDB, la contraseña de Redis, las llaves de datos personales, CURP, TOTP y dispositivos, ni la cuenta de servicio de Firebase. Cambiar `jwt-signing-key` no equivale a revocar sesiones: el token de renovación no es un JWT.
- `deploy/apps` no define las variables `Previous` de las claves de servicio.
- No hay un procedimiento de nuevo cifrado de los datos guardados.

### 4.2 Sesiones
Rutas descritas en [`endpoints.md`](endpoints.md):

| Caso | Mecanismo que existe |
|---|---|
| La persona cierra su sesión actual | `POST /api/v1/auth/logout` |
| La persona cierra otra sesión suya | `GET /api/v1/me/sesiones` y `DELETE /api/v1/me/sesiones/{id}` |
| Reuso de un token de renovación ya usado | Automático: revoca todas las sesiones de la cuenta (app y panel; el panel tolera una renovación concurrente de hasta 10 segundos) |
| Cambio de número confirmado | Cierra las demás sesiones |
| Técnico con un reporte de gravedad crítica | La decisión del panel (`POST /api/v1/admin/reportes/{id}/decision`) lo suspende y revoca sus sesiones. Requiere un reporte previo y no aplica a clientes |
| Cuenta del panel comprometida | Herramienta de línea de comandos de Identidad: `panel deshabilitar` o `panel reiniciar-totp` (spec 033, R-25). Revoca las sesiones del panel de esa cuenta, no las de la app |
| Cerrar la sesión del panel propia | `POST /api/v1/panel/sesion/cerrar` |

La cancelación de un token de acceso vive en Redis (`sesion-cancelada:{sid}`). **Si Redis se reinicia o se recrea, un token ya revocado vuelve a servir hasta que vence** (`deploy/README.md`).

**Huecos:** no hay una acción de administración para revocar las sesiones de una cuenta cualquiera ni de todas las cuentas; no se puede suspender a un cliente; no está documentado cómo ejecutar la herramienta `panel` contra el ambiente desplegado.

### 4.3 Apagar funciones
Se cambian en la variable de Terraform de `deploy/apps` y se aplican como en el paso 2 de `deploy/README.md`. Antes de aplicar, revisar el `plan` (no debe destruir nada que no se quiera) y el valor que el ambiente tiene hoy.

| Función | Variable | Configuración | Apagada |
|---|---|---|---|
| Asistente con Azure OpenAI | `enable_azure_openai` | `Asistente:Habilitado` | El asistente responde con la plantilla |
| Moderación con Content Safety | `enable_content_safety` | `Moderacion:Habilitado` | Mercado falla cerrado. Debe valer lo mismo en `deploy/infra`, donde `false` afecta al recurso: revisar ese plan aparte |
| Avisos push | `enable_push` | `Notificaciones:PushHabilitado` | No se envían avisos push |
| Reparto por oleadas (encendido por omisión) | `enable_oleadas` | `Oleadas:Habilitado` | El reparto no corre |
| Verificación de CURP con Key Vault | `enable_curp_keyvault` | `Verificacion:KeyVault:Habilitado` | Guardar verificaciones responde 503 |

No se enciende `Moderacion:PermitirSinModeracion` para salir de un incidente.

**Chat:** la aplicación admite `Chat:Habilitado=false` (spec 026, R-17: el saludo responde 503 `chat_disabled` y las conexiones abiertas se cierran al terminar su token). **Hueco:** `deploy/apps` lo fija en `true`, sin variable; apagarlo exige cambiar la configuración del servicio y no hay un procedimiento escrito.

**Último recurso:** el paso 3 de `deploy/README.md` (`terraform destroy` en `deploy/apps`) deja el backend fuera de servicio y conserva la infraestructura y la base. Redis se pierde. Bajar el mínimo de réplicas a 0 **no** apaga un servicio: despierta con la siguiente petición. Nunca se destruye `deploy/database`. Antes, valorar qué registros hace falta guardar.

**Hueco:** no hay un procedimiento para bloquear una dirección IP o un cliente concreto; existen los límites de peticiones del gateway ([`rate-limit-matrix.md`](rate-limit-matrix.md)).

### 4.4 Volver a una imagen anterior
1. Elegir el SHA completo anterior en [`VERSIONES_DESPLEGADAS.md`](VERSIONES_DESPLEGADAS.md) y comprobar que no trae el mismo problema.
2. Una persona con permiso lanza `ci-deploy` a mano (`workflow_dispatch`) con ese SHA en `image_tag`. El flujo valida el formato, exige que **las cuatro** imágenes existan con esa etiqueta y actualiza los cuatro servicios.
3. La prueba de humo **no corre** en el disparo manual: comprobar `/health` y `/health/ready` del gateway y el estado de las revisiones.

Volver de imagen no revierte datos, variables ni secretos. Terraform ignora los cambios de imagen, así que `terraform apply` no hace esta reversión.

**Huecos:**
- La reversión de la spec 034 (R-09) no tiene evidencia de ensayo en `docs/evidencia/`, y al 2026-11-28 GitHub no listaba ninguna ejecución manual de `ci-deploy`.
- **Puede fallar al iniciar sesión en Azure (no probado):** el job declara `environment: evaluacion`, y con un entorno el sujeto OIDC de GitHub es `repo:<organización>/<repositorio>:environment:evaluacion`, no el de la rama ([referencia de GitHub](https://docs.github.com/en/actions/reference/security/oidc)). `deploy/README.md` solo documenta credenciales federadas para la rama `main`.
- Al 2026-11-28 el repositorio no tenía entornos configurados: no hay una revisión obligatoria para esa ejecución.
- `ci-deploy` publica solo los servicios que cambiaron, así que un SHA puede no tener las cuatro imágenes.

Si el flujo no sirve, una persona con permiso en Azure puede usar el mismo comando que el flujo, por servicio: `az containerapp update --name knock-<servicio> --resource-group <grupo> --image <registro>/knock-<servicio>:<SHA>`.

### 4.5 Corregir
La corrección entra por pull request con su prueba y el CI en verde; no se saltan controles. Un arreglo urgente sigue la rama `hotfix/*` de [`CONTRIBUTING.md`](../CONTRIBUTING.md).

Para saber si una dependencia vulnerable está incluida, `ci-pr` publica un SBOM CycloneDX como artefacto: `sbom-dotnet` (paquetes NuGet de los `packages.lock.json`, incluidos los de las pruebas) y `sbom-<imagen>` por cada imagen que esa ejecución construyó. **Límites:** describen lo construido por `ci-pr`, no la imagen publicada, que `ci-deploy` vuelve a construir. **Hueco:** las imágenes no tienen firma ni atestación de procedencia.

## 5. Comunicación
- El repositorio es **público**. Mientras el problema siga abierto, no se publican detalles de explotación, datos afectados ni evidencia en issues, PR, commits o documentos.
- El equipo se entera por el Tech Lead, por un canal privado. **Hueco:** el canal no está definido.
- A quien reportó se le responde según `SECURITY.md`.
- **Hueco:** no está definido cuándo ni cómo avisar a las personas afectadas, al profesorado, a una autoridad o a los proveedores, ni qué obligaciones legales aplican. Lo decide el responsable del proyecto; hasta entonces, ningún integrante hace un aviso externo por su cuenta.
- **Hueco:** no hay un procedimiento para publicar una corrección de seguridad sin revelar antes el fallo.

## 6. Evidencia
Guardar, con fecha y hora en UTC:
- la alerta o el aviso original;
- consultas y resultados de los registros, con sus `correlationId` y `traceId`;
- los registros de auditoría del panel relacionados;
- el commit, el SHA de las imágenes desplegadas, las ejecuciones de GitHub Actions, las revisiones de Container Apps y el SBOM disponible;
- cada acción de contención: quién, cuándo, qué y con qué resultado.

Reglas: nunca copiar valores de secretos ni datos personales; usar identificadores. Descargar pronto lo necesario, porque ni los registros ni los artefactos son un archivo permanente. La evidencia de un incidente no se sube al repositorio.

**Huecos:** no está definido dónde se guarda la evidencia privada ni quién accede a ella. Los plazos de conservación son una decisión pendiente del responsable del proyecto y este documento no los fija.

## 7. Cierre y revisión posterior
Un incidente se cierra cuando la contención está comprobada, la corrección está desplegada, las credenciales afectadas se rotaron y el Tech Lead lo confirma. Después se hace una revisión corta, sin buscar culpables:

1. Qué pasó y línea de tiempo (inicio, detección, contención, corrección).
2. Severidad, alcance y datos afectados.
3. Cómo se detectó y qué habría permitido detectarlo antes.
4. Causa raíz.
5. Qué funcionó, qué no y qué hueco de este plan apareció.
6. Acciones, con responsable y tarjeta de Jira.

Las lecciones se registran en [`memoria/`](memoria/README.md) y la amenaza en [`modelo-de-amenazas.md`](modelo-de-amenazas.md), sin datos sensibles y solo cuando publicarlas ya no exponga el sistema. Si cambia un procedimiento, se actualiza este documento.
