# Métricas y alerta de la bandeja de salida y de las tareas (spec 054, T-07)

## Qué cambió
- Identidad y Mercado publican el medidor `Knock.Procesos`: `knock.procesos.intentos` (cada entrega o ejecución) y `knock.procesos.alertas` (fallidos y con más de 10 minutos de retraso), con proceso, tipo, identificador, intento y resultado.
- Un proceso de fondo evalúa la alerta cada `TareasProgramadas:IntervaloSegundos` y registra `process_alert` una vez por elemento. Detalle en `docs/OBSERVABILIDAD.md`.
- La consulta vive en `Knock.Hosting.Data` (`ConsultaDeAlertasMongo`) para no copiarla en cada servicio.

## Por qué
- Cierra la parte de código de R-13 y R-14. La alerta se consulta en la base, no solo al procesar, porque un elemento atrasado puede no reservarse nunca.
- El identificador va como atributo de la métrica porque R-13 y CA-20 lo piden.

## A evitar
- Poner la carga, `datos` o `claveNegocio` en métricas o registros.
- Registrar o etiquetar el `_id` de la bandeja tal cual: puede llevar `sub`, `sid` o el id de la persona. Se usa `ProcesosMetrics.Identificador`.
- Dar por avisado a alguien: la regla de Terraform no existe todavía.
- Mercado todavía no escribe `limpiarEn` (R-11) en esta rama: lo agrega el PR #415, aún sin fusionar; hasta entonces un `fallido` suyo no se borra y sigue en alerta. Además, sus eventos sin entrega registrada alertan a los 10 minutos.
