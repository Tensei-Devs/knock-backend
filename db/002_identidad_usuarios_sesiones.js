// Spec 015 (KNOCK-210): colecciones `usuarios` y `sesiones` del servicio de Identidad y sus índices.
// Uso (igual que 001_indices.js, y como lo aplica db/entrega.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/002_identidad_usuarios_sesiones.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// La base se toma de KNOCK_DB_IDENTITY (por defecto «identity»; KNOCK_IDENTITY_DB sigue valiendo). Es idempotente: volver a correrlo con las mismas
// definiciones no cambia nada. Las definiciones deben coincidir con SesionesStore.EnsureIndexesAsync.
const dbName = process.env.KNOCK_DB_IDENTITY || process.env.KNOCK_IDENTITY_DB || "identity";
const identity = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(dbName) : db.getSiblingDB(dbName);

for (const name of ["usuarios", "sesiones"]) {
  if (!identity.getCollectionNames().includes(name)) {
    identity.createCollection(name);
  }
}

// usuarios: una cuenta por `sub` (derivado del uid de Firebase) y una por celular (HMAC-SHA256 del número, nunca el número).
identity.usuarios.createIndex({ sub: 1 }, { unique: true, name: "sub_1" });
identity.usuarios.createIndex({ celularHash: 1 }, { unique: true, name: "celularHash_1" });

// sesiones: la huella del token de renovación es única; `venceEn` expira el documento al llegar la fecha (TTL);
// el índice compuesto sirve para contar y revocar las sesiones abiertas de una cuenta (máximo 10).
identity.sesiones.createIndex({ huellaRenovacion: 1 }, { unique: true, name: "huellaRenovacion_1" });
identity.sesiones.createIndex({ venceEn: 1 }, { expireAfterSeconds: 0, name: "venceEn_1" });
identity.sesiones.createIndex({ usuarioId: 1, revocada: 1, creadoEn: 1 }, { name: "usuarioId_1_revocada_1_creadoEn_1" });

print(`Índices de identidad listos en «${dbName}»`);
