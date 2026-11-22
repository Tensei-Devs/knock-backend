// Spec 044 (KNOCK-105): colecciones `avisos` y `preferencias_avisos` del servicio de Mensajería y sus índices.
// Uso (igual que 030-conversaciones.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/044-avisos-dentro-de-la-app.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// La base se toma de KNOCK_DB_MESSAGING (por defecto «messaging»). Es idempotente: crea lo que falta y nunca borra ni cambia colecciones, índices ni documentos existentes.
// La definición debe coincidir con spec 044 §6 y con NoticesStore. Los avisos se conservan 30 días: `venceEn` lo asigna quien crea el aviso (el despachador de la spec 037) y el TTL lo borra al vencer.
const avisosDbName = process.env.KNOCK_DB_MESSAGING || "messaging";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(avisosDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const inAppMessaging = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(avisosDbName) : db.getSiblingDB(avisosDbName);

for (const collection of ["avisos", "preferencias_avisos"]) {
  if (!inAppMessaging.getCollectionNames().includes(collection)) {
    inAppMessaging.createCollection(collection);
  }
}

// avisos: lista por persona (con filtro de leídos) y contador de no leídos; vencimiento por `venceEn` (TTL 0: vence en la fecha del documento).
inAppMessaging.avisos.createIndex({ usuarioId: 1, leido: 1, creadoEn: -1 }, { name: "usuarioId_1_leido_1_creadoEn_-1" });
inAppMessaging.avisos.createIndex({ venceEn: 1 }, { expireAfterSeconds: 0, name: "venceEn_1" });

// preferencias_avisos: un documento por persona.
inAppMessaging.preferencias_avisos.createIndex({ usuarioId: 1 }, { name: "usuarioId_1", unique: true });

print(`Índices de avisos y preferencias_avisos listos en «${avisosDbName}»`);
