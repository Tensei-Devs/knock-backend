# Servicios y precios de referencia del técnico (KNOCK-57)

## Qué cambió
Mercado expone SRV-01 a SRV-04 (spec 047): `GET /api/v1/tecnicos/{id}/servicios` y `POST`, `PUT`, `DELETE /api/v1/tecnicos/me/servicios`. Colección `servicios_tecnico` con script `db/047-servicios-tecnico.js`; máximo 50 por técnico (409 `service_limit_reached`); el oficio debe ser del técnico y estar activo; un servicio ajeno responde 404 `service_not_found`.

## Por qué
HU-018 pide servicios con precio en el perfil público. El precio es solo de referencia (R-17).

## A evitar
Aceptar el identificador del técnico en el cuerpo, o revelar que un servicio ajeno existe.

## Decidido
Sin caché de 1 minuto (R-04) por ahora: la lectura es de a lo más 50 documentos indexados y una caché por réplica daría datos viejos tras escribir. Alternativa: caché en Redis con invalidación por técnico. PARA VALIDACIÓN DE JOSÉ.