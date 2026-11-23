# Spec 056: validación única de la credencial de servicio

## Qué cambió
- Nueva pieza `Knock.Hosting.Security` (`ServiceCredential`, `ServiceCredentialSettings`, `AddKnockServiceCredential`) con la validación única de la credencial de servicio: HS256, clave por emisor, `iss`, `aud`, `jti`, vida de 5 minutos como máximo, `ClockSkew` en cero y falla cerrada sin clave válida.
- `InternalServiceAuthentication` (Mercado), `InternalNoticeAuthentication` (Mensajería) e `InternalServiceTokenAttribute` (Identidad) dejaron de tener su propia copia y delegan en ella; sus constantes públicas, políticas, códigos y contrato no cambian.
- Rotación (D-01, D-02): `<Clave>:Previous` es la clave anterior y `<Clave>:PreviousUntil` el instante hasta el que vale (ventana de 24 horas). Procedimiento en `docs/rotacion-de-claves-de-servicio.md`.
- Dependencia nueva en `Knock.Hosting`: `Microsoft.AspNetCore.Authentication.JwtBearer` 11.0.0, la misma versión de los servicios. Enmienda del catálogo en la spec 055 (§14, punto 5), pendiente de las dos aprobaciones.
- Pruebas: `ServiceCredentialTests` (CA_056_01, 02, 04, 05, 06, 10, 11), `Spec056RutasInternasTests` (CA_056_07) y una prueba de repetición por ruta existente (Historial de Mercado y avisos de Mensajería).

## Por qué
Había tres copias de la misma regla de seguridad; un arreglo (por ejemplo la rotación) tenía que repetirse tres veces. La spec 056 T-02 pide una sola validación, y la 055 la admite como seguridad transversal sin reglas de negocio.

## A evitar
- Dejar `<Clave>:Previous` sin `PreviousUntil`: la clave anterior no se acepta (falla cerrado).
- Reintroducir una validación propia de la credencial en un servicio: se mueve a `Knock.Hosting.Security`.
- Guardar el `jti` en Redis: P-03 lo descarta; la repetición dentro de los 5 minutos se acepta y cada receptor es idempotente.
