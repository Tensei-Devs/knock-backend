// Spec 029 (KNOCK-71, T-06): comprueba los índices que usa el vencimiento y programa el barrido de respaldo `BarrerVencidas` si no hay uno pendiente.
// Uso: KNOCK_DB_URI=... mongosh --nodb --quiet --file db/029-requests-vencimiento.js   (o con `mongosh "<cadena>" --file …`).
// Idempotente: no borra ni cambia documentos existentes; reaplicarlo no duplica la tarea.
const expiryDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(expiryDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const expiryMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(expiryDbName) : db.getSiblingDB(expiryDbName);
for (const name of ["solicitudes", "tareas_programadas"]) {
  if (!expiryMarket.getCollectionNames().includes(name)) {
    expiryMarket.createCollection(name);
  }
}

// El barrido busca por { estado, venceEn } (db/020) y la tarea es única por { tipo, claveNegocio } (db/028).
expiryMarket.solicitudes.createIndex({ estado: 1, venceEn: 1 }, { name: "estado_1_venceEn_1" });
expiryMarket.tareas_programadas.createIndex({ tipo: 1, claveNegocio: 1 }, { unique: true });

const now = new Date();
const pending = expiryMarket.tareas_programadas.countDocuments({ tipo: "BarrerVencidas", estado: { $in: ["pendiente", "en_proceso"] } });
if (pending === 0) {
  expiryMarket.tareas_programadas.updateOne(
    { tipo: "BarrerVencidas", claveNegocio: "recurrente:inicial" },
    { $setOnInsert: { programadaPara: now, estado: "pendiente", intentos: 0, creadoEn: now, actualizadoEn: now } },
    { upsert: true });
}

print(`Vencimiento de solicitudes listo en «${expiryDbName}»`);
