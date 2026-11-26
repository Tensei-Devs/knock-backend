# Colección archivos de Media: pruebas de seguridad y concurrencia y cierre (spec 058, T-06 y T-07)

## Qué cambió
- `Spec058ArchivosSeguridadTests` cubre CA-06 (archivo ajeno e inexistente dan el mismo `NotFound` y `false`), CA-17 (el dueño sale solo del token y el DTO no declara campo de dueño), CA-18 (1 000 `archivoId` UUID v7 de 32 hexadecimales y distintos) y CA-19 (los contadores no llevan etiquetas y el ticket solo expone `Id`).
- `Spec058TopePendientesTests` repite 30 veces las ráfagas de 25 subidas simultáneas (CA-08) y de 8 + 5 (CA-09): nunca quedan más de 10 pendientes. `Spec058MongoMediaFileStoreTests` añade la misma ráfaga contra MongoDB real con dos instancias del almacén simulando dos réplicas.
- Flujo privado (para la documentación de endpoints cuando existan las rutas): `MediaService.CreateUploadAsync` devuelve `LimitExceeded` con `RetryAfterSeconds` entre 1 y 1 800; la capa HTTP lo traduce a 429 `limit_exceeded` con `Retry-After`. No se guarda nada al rechazar y el cuerpo no repite valores del cliente.

## Por qué
La spec 058 pide demostrar que el tope y el dueño no se saltan con peticiones simultáneas ni adivinando identificadores, y que los contadores no filtran datos (R-05, R-10, R-12, R-13). El almacén en memoria serializa con un candado, por eso la carrera real se prueba también contra MongoDB.

## A evitar
- Contar y luego insertar: solo «insertar y verificar» garantiza el tope entre réplicas.
- Añadir etiquetas (`sub`, `blobKey`) a los contadores de `Knock.Media`.
- Cerrar la 058 sin T-08 (validador de `fotos` de 32 hexadecimales, `Spec058ContractTests` y OpenAPI) ni T-09 (Azure, lo hace una persona): T-07 queda a la espera de ellos; `docs/endpoints.md` y la matriz de límites se actualizan en el cierre del backend cuando existan las rutas privadas.
