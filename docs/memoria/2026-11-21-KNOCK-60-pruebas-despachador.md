# KNOCK-60 — pruebas de integración y seguridad del despachador de avisos (spec 037 T-05 a T-07 parcial)

## Qué cambió
- `Spec037DespachoIntegrationTests`: despachador con MongoDB y Redis reales y un envío FCM simulado (deduplicación concurrente, carga v1, presencia y ventana de 5 minutos, identificador inválido borrado, tres reintentos, dispositivo ilegible sin filtrar valores y carga de 1 000 eventos a 200 personas sin pérdidas ni duplicados).
- `Spec037InternoAvisosSeguridadTests`: `POST /api/v1/interno/avisos` rechaza sin token, con clave ajena, con token de persona, sin permiso del tipo, con vida mayor de 5 minutos, con campos extra y con rol que no corresponde; dispositivos sin sesión responden 401.
- `Spec037ArquitecturaTests` (el despachador no depende de Mongo, Redis ni otros módulos) y `Spec037CargaV1ContratoTests` (forma exacta de la carga v1).
- `DeviceStore` reintenta también el bloqueo mutuo de DocumentDB entre inserciones concurrentes del mismo identificador, además del conflicto 112.
- `Spec037DeviceAvailabilityTests` se alineó con el comportamiento vigente: el último registro gana (201) y `DELETE` responde 204 siempre.

## Por qué
La spec pide probar el despachador con bases reales antes de conectar FCM; así el envío real solo cambia el `IPushSender`. El bloqueo mutuo hacía fallar al azar el registro concurrente.

## A evitar
No buscar un valor corto dentro del cuerpo crudo en pruebas con valores aleatorios; no lanzar miles de tareas sin acotar contra MongoDB (cola de conexiones llena); no activar `Notificaciones:PushHabilitado` sin credenciales de Firebase en el almacén del despliegue (bloqueo: permiso, credenciales FCM, José).
