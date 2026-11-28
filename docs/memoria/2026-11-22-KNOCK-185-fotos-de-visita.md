# Memoria — KNOCK-185 (adaptador de fotos de la visita, specs 032 y 050)

## Qué cambió
- `MediaVisitPhotos` (Requests/Services/VisitPhotos.cs) adapta `IMediaApi` a `IVisitPhotos` y reemplaza a `UnavailableVisitPhotos` como registro por omisión; terminar una visita ya no responde 422 por falta de adaptador.
- Las fotos de `POST /visitas/{id}/terminar` se identifican con el `archivoId` del módulo de archivos (32 hexadecimales, `VisitCloseRules.TryParsePhotoId`), no con un ObjectId de 24; validador, servicio y pruebas actualizados.
- Pruebas unitarias del adaptador (`Spec032MediaVisitPhotosTests`).

## Por qué
- El módulo de archivos identifica cada foto con un UUID v7 (spec 058, R-01); un ObjectId nunca podría estar «listo», así que la ruta no podía funcionar con el formato anterior.
- El adaptador falla cerrado: identificador que no es de archivo, archivo ajeno o no listo, o fallo del módulo de archivos dan `false` (la visita no se termina). Solo viajan ids y el `sub`; nada de enlaces ni datos personales.

## A evitar
- Aceptar ids de 24 hexadecimales en las fotos de la visita: nunca coinciden con un archivo.
- Capturar `OperationCanceledException` en el adaptador: la cancelación debe propagarse.
- Registrar en el log el contenido de la excepción o los ids; solo tipo y conteo.
- Pendiente: `IMediaApi` no revisa el uso (`Job`) del archivo; si la spec lo exige, extender el contrato público. La spec 032 T-10 (humo en Azure) necesita permiso y no se hizo.
