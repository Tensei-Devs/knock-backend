// Spec 057 (KNOCK-87): campos de la purga de fotos en `verificaciones` (alertadaEn, purgadaEn, razonPurga).
// KNOCK_DB_URI=... mongosh --nodb --quiet --file db/057-identity-verificaciones-purga.js
// La base se toma de KNOCK_DB_IDENTITY (por defecto «identity»). Es idempotente: solo completa con null los documentos que no traen el campo y nunca
// cambia uno que ya lo tiene. No crea índices (la búsqueda de vencidas usa estado_1_expiraEn_1 de la spec 025) ni edita db/025-verificaciones.js.
const purgeDbName = process.env.KNOCK_DB_IDENTITY || "identity";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(purgeDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}
const purgeIdentity = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(purgeDbName) : db.getSiblingDB(purgeDbName);

if (!purgeIdentity.getCollectionNames().includes("verificaciones")) {
  purgeIdentity.createCollection("verificaciones");
}

for (const purgeField of ["alertadaEn", "purgadaEn", "razonPurga"]) {
  purgeIdentity.verificaciones.updateMany({ [purgeField]: { $exists: false } }, { $set: { [purgeField]: null } });
}

print(`Campos de purga de verificaciones listos en «${purgeDbName}»`);
