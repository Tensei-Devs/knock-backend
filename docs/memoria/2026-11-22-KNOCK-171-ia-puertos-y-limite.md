# IA apagada: puertos reales del asistente y tope de moderación en los llamadores (specs 052 y 031)

## Qué cambió
- El asistente (spec 052) ya consulta Tecnicos (`ITecnicosApi.HasProfileAsync`) y Requests (`IRequestsApi.GetTechnicianAccessAsync`) en lugar de respaldos que siempre fallaban. Si el contrato no está registrado se falla cerrado.
- El adaptador de moderación distingue el tope por persona (`ModerationOutcome.LimitReached`) de la falla: solicitudes e imágenes responden 429 `moderation_limit` con `Retry-After` hasta la medianoche; el 503 de solicitudes también lleva `Retry-After`.
- Política de límite del asistente movida a `MarketRateLimits` tras el limitador compartido. Los interruptores `Asistente:Habilitado` y `Moderacion:Habilitado` siguen apagados por omisión; no se creó ningún recurso de Azure.

## Por qué
La spec 052 pide que solo técnicos participantes lean y escriban su bitácora, y la 031 (R-13) pide una sola traducción de la decisión a HTTP en todos los llamadores.

## A evitar
- Leer colecciones de otro módulo desde el asistente: solo por contratos públicos.
- Tratar el tope por persona como falla del proveedor (503): la persona debe saber que vuelva mañana.

## Pendiente
- Copia de moderación en Mensajería (031 T-08, requiere excepción de duplicación del líder), referencia de mercado (048), conversaciones (030) y recurso de Azure OpenAI (bloqueo: costo).
