// Spec 053 (KNOCK-86): índice de la cola de revisión de verificaciones (§6). La cola de reportes usa el índice
// `gravedad_1_estado_1_creadoEn_1` de db/043-reportes.js, que no se repite aquí.
// Uso (igual que 025-verificaciones.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/053-panel-de-revision.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// La base se toma de KNOCK_DB_IDENTITY (por defecto «identity»). Es idempotente: crea lo que falta y nunca borra ni cambia nada.
// La definición debe coincidir con VerificationReviewStore.EnsureIndexesAsync.
const reviewDbName = process.env.KNOCK_DB_IDENTITY || "identity";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(reviewDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const reviewIdentity = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(reviewDbName) : db.getSiblingDB(reviewDbName);

if (!reviewIdentity.getCollectionNames().includes("verificaciones")) {
  reviewIdentity.createCollection("verificaciones");
}

// Cola del panel, de la más antigua a la más reciente, y alerta de las 24 horas (spec 053 §6).
reviewIdentity.verificaciones.createIndex({ estado: 1, enviadoEn: 1 }, { name: "estado_1_enviadoEn_1" });

print(`Índice de la cola de revisión listo en «${reviewDbName}»`);
