// Spec 015 (KNOCK-210): colecciones `usuarios` y `sesiones` del servicio de Identidad y sus índices.
// Uso: mongosh "<cadena de conexión>" --file db/002-identidad-usuarios-sesiones.js
// La base se toma de la variable de entorno KNOCK_IDENTITY_DB (por defecto «identity»). Es idempotente: volver a correrlo con las mismas
// definiciones no cambia nada. Las definiciones deben coincidir con SesionesStore.EnsureIndexesAsync.
const dbName = process.env.KNOCK_IDENTITY_DB || "identity";
const identity = db.getSiblingDB(dbName);

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
