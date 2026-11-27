# Pruebas de seguridad por ruta para sesiones, direcciones, verificación y técnicos de confianza

## Qué cambió
- Sin token o con token vencido: 401 en las rutas de sesiones, direcciones, verificación y técnicos de confianza.
- Entre dos personas: una sesión o una dirección ajena responde 404 y no cambia; nadie quita un técnico guardado por otra persona.
- Cada caso entre personas tiene un control: quien es dueño del dato sí puede actuar.

## Por qué
- Eran rutas con datos personales sin pruebas en `tests/SecurityTest`.

## A evitar
- Probar una ruta o un verbo que no existe en el contrato.
- Esperar 404 sin un control del dueño: un dato mal sembrado también da 404.

## Pendiente
- No hay casos entre personas para la verificación de identidad ni pruebas de rol.
