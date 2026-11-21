// Spec 058 (T-01): colección `archivos` del módulo Media (registro de archivos privados) en la base de Mercado y sus índices.
// Uso (igual que 044-market-subidas-imagen.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/049-market-archivos.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// La base se toma de KNOCK_DB_MARKET (por defecto «market»). Es idempotente: crea lo que falta y nunca borra ni cambia colecciones, índices ni documentos existentes.
// archivoId_1 es único (identificador de negocio, UUID v7); propietarioId_1_estado_1_confirmarAntesDe_1 cubre los archivos de una persona y el conteo de pendientes vigentes;
// expiraEn_1 es el TTL (expireAfterSeconds 0): pendiente y rechazado llevan creadoEn + 7 días y un archivo listo se fija a 9999-12-31 para que no venza nunca (R-07).
const archivosDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(archivosDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const archivosMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(archivosDbName) : db.getSiblingDB(archivosDbName);

if (!archivosMarket.getCollectionNames().includes("archivos")) {
  archivosMarket.createCollection("archivos");
}

archivosMarket.archivos.createIndex({ archivoId: 1 }, { unique: true, name: "archivoId_1" });
archivosMarket.archivos.createIndex({ propietarioId: 1, estado: 1, confirmarAntesDe: 1 }, { name: "propietarioId_1_estado_1_confirmarAntesDe_1" });
archivosMarket.archivos.createIndex({ expiraEn: 1 }, { expireAfterSeconds: 0, name: "expiraEn_1" });

print(`Colección archivos lista en «${archivosDbName}»`);
