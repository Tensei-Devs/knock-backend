// Spec 054 (T-01): índices de `outbox` y `tareas_programadas` en las bases de Identidad, Mercado y Mensajería (§6).
// Uso (igual que 053-panel-de-revision.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/054-outbox-y-tareas-programadas.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// Las bases se toman de KNOCK_DB_IDENTITY, KNOCK_DB_MARKET y KNOCK_DB_MESSAGING (por omisión «identity», «market» y «messaging»).
// Es idempotente: crea lo que falta y nunca borra ni cambia documentos.
// Limpieza (R-11, D-02): al pasar a un estado terminal el proceso escribe `limpiarEn` (actualizadoEn + 14 días para `hecha` y `entregado`,
// + 90 días para `fallida` y `fallido`) y un solo índice TTL con expireAfterSeconds 0 lo elimina; un TTL exige un solo campo y así no hacen falta dos índices parciales sobre la misma clave.
const outboxDatabases = [
  process.env.KNOCK_DB_IDENTITY || "identity",
  process.env.KNOCK_DB_MARKET || "market",
  process.env.KNOCK_DB_MESSAGING || "messaging",
];
for (const name of outboxDatabases) {
  if (!/^[A-Za-z0-9_-]{1,63}$/.test(name)) {
    print("Nombre de base no válido (letras, dígitos, '_' o '-').");
    quit(2);
  }
}

const outboxClient = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI) : null;
for (const name of outboxDatabases) {
  const target = outboxClient ? outboxClient.getDB(name) : db.getSiblingDB(name);
  for (const collection of ["outbox", "tareas_programadas"]) {
    if (!target.getCollectionNames().includes(collection)) {
      target.createCollection(collection);
    }
  }

  target.outbox.createIndex({ estado: 1, programadaPara: 1 }, { name: "estado_1_programadaPara_1" });
  target.outbox.createIndex({ limpiarEn: 1 }, { name: "limpiarEn_1", expireAfterSeconds: 0 });
  target.tareas_programadas.createIndex({ estado: 1, programadaPara: 1 }, { name: "estado_1_programadaPara_1" });
  target.tareas_programadas.createIndex({ tipo: 1, claveNegocio: 1 }, { name: "tipo_1_claveNegocio_1", unique: true });
  target.tareas_programadas.createIndex({ limpiarEn: 1 }, { name: "limpiarEn_1", expireAfterSeconds: 0 });
  print(`Índices de outbox y tareas_programadas listos en «${name}»`);
}
