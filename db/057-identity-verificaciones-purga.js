// Spec 057 (T1): campos de la purga de fotos de verificación en `verificaciones` (§5).
// Uso (igual que 053-panel-de-revision.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/057-identity-verificaciones-purga.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta. La base se toma de KNOCK_DB_IDENTITY (por omisión «identity»).
// Es idempotente: solo agrega, con valor null, los campos `alertadaEn`, `purgadaEn` y `razonPurga` a los documentos que no los tienen; nunca borra ni cambia otros campos.
// No edita db/025-verificaciones.js. Sin índice nuevo: la búsqueda de vencidas usa { estado: 1, expiraEn: 1 } de la spec 025.
const purgeDbName = process.env.KNOCK_DB_IDENTITY || "identity";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(purgeDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const purgeClient = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI) : null;
const purgeIdentity = purgeClient ? purgeClient.getDB(purgeDbName) : db.getSiblingDB(purgeDbName);
if (!purgeIdentity.getCollectionNames().includes("verificaciones")) {
  purgeIdentity.createCollection("verificaciones");
}

for (const purgeField of ["alertadaEn", "purgadaEn", "razonPurga"]) {
  purgeIdentity.verificaciones.updateMany({ [purgeField]: { $exists: false } }, { $set: { [purgeField]: null } });
}

print(`Campos de purga de verificaciones listos en «${purgeDbName}»`);
