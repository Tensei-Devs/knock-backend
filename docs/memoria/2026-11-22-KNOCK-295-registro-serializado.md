# KNOCK-295: registro de técnico simultáneo sin 500 (CA-046-69, causa raíz)

## Qué cambió

- `TechnicianProfileStore.InsertAsync` ahora turna, dentro de la instancia, los registros de una misma cuenta (candado por franja del `sub`) y, ya dentro del turno, relee el perfil: si existe responde «ya existía» (409) sin escribir; si no, inserta con el reintento que ya existía.
- La prueba de integración CA-046-69 guarda las excepciones que llegan al manejador global y las incluye en su mensaje de fallo.

## Por qué

- Cuatro `insert` simultáneos sobre la misma llave única de `usuarioId` se bloquean entre sí en documentdb-local («Could not acquire lock for operation due to deadlock», código 24). Los reintentos con espera de 50 a 250 ms se agotaban antes de que el bloqueo se liberara y la excepción llegaba como 500. Se reprodujo en local con ráfagas repetidas sobre un mismo host (la corrida de 60 rondas tardaba 4 minutos y fallaba; con el turno tarda 21 segundos y pasa).
- El log de la prueba salía vacío porque la excepción no controlada no pasa por el proveedor de log añadido en la prueba; el manejador de excepciones sí la ve.

## A evitar

- No subir el número de reintentos ni la espera para tapar el interbloqueo: solo alarga cada petición.
- El turno es por instancia; entre varias instancias siguen valiendo la llave única, la relectura y el reintento de `InsertOnceAsync`.
