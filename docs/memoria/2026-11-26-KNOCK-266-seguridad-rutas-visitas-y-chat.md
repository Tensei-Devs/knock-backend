# Pruebas de acceso entre personas: solicitudes, visitas, conversaciones, avisos, exportación y verificaciones

## Qué cambió
- `tests/SecurityTest` cubre por ruta las solicitudes y sus cotizaciones y las visitas con sus acciones: sin token, con token vencido, persona ajena y rol incorrecto.
- Conversaciones y avisos: otra persona no lee ni cambia lo ajeno. Verificaciones del panel: solo el rol de verificación abre y decide. Exportación: cada persona ve la suya.
- Apoyos nuevos: `MarketAccessHost`, `AccessCase`, `MessagingAccessHost` y `MongoCollections`.

## Por qué
- Eran rutas con datos personales sin casos entre personas en `tests/SecurityTest`. Parte ya se probaba en `tests/IntegrationTest` (solicitudes, comparar, elegir, cancelar, ampliar, ver la visita, llegada, aviso, varias acciones de visita y lectura de conversaciones).
- La búsqueda por texto de la ruta no ve las constantes: las verificaciones del panel y la exportación ya tenían pruebas de 401 o 403.
- En conversaciones, el Mercado de las pruebas concede siempre el permiso: así el rechazo solo puede venir de la comprobación de participantes.

## A evitar
- Probar a una persona ajena sin sembrar el recurso: el 404 saldría igual sin control de propiedad.
- Rechazar sin control: cada caso repite la misma petición con quien tiene derecho y compara los datos antes y después.
- Probar el rol de técnico con una cuenta sin documento en `tecnicos` como único ajeno: hace falta además un técnico real sin relación.

## Pendiente
- La spec 044 pide 403 en CA-09 y 404 en CA-11 y CA-18 para un aviso ajeno; el código responde 404 y las pruebas siguen CA-11 y CA-18.
- Fuera de este cambio: opiniones, `cotizaciones/{id}/ajuste-precio`, `ajustes/{id}/decision` y las rutas internas.
