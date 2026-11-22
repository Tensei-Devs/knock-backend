// Spec 059 (T-01): documenta el subdocumento opcional `usuarios.suspension` (§6) y verifica el estado que la suspensión espera.
// Uso (igual que 057-identity-verificaciones-purga.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/059-suspension-cuenta.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta. La base se toma de KNOCK_DB_IDENTITY (por omisión «identity»).
//
// Forma del campo (solo existe mientras `estado = "suspendida"`; la escribe y la borra el servicio, nunca este script):
//   usuarios.suspension = { tipo: "preventiva" | "gradual", reporteId: ObjectId, desde: Date (UTC, asignada por el servidor) }
//
// Es idempotente y de solo lectura sobre los datos: no agrega índices (no hay consulta por `suspension`), no cambia documentos y no borra nada. Solo crea la colección `usuarios`
// si no existe y avisa de cuentas con estado y subdocumento inconsistentes (suspendida sin `suspension`, o `suspension` con otro estado) para que una persona las revise.
const suspensionDbName = process.env.KNOCK_DB_IDENTITY || "identity";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(suspensionDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const suspensionClient = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI) : null;
const suspensionIdentity = suspensionClient ? suspensionClient.getDB(suspensionDbName) : db.getSiblingDB(suspensionDbName);
if (!suspensionIdentity.getCollectionNames().includes("usuarios")) {
  suspensionIdentity.createCollection("usuarios");
}

const suspendedWithoutDetail = suspensionIdentity.usuarios.countDocuments({ estado: "suspendida", suspension: { $exists: false } });
const detailWithoutSuspension = suspensionIdentity.usuarios.countDocuments({ estado: { $ne: "suspendida" }, suspension: { $exists: true } });
if (suspendedWithoutDetail > 0 || detailWithoutSuspension > 0) {
  print(`Aviso: ${suspendedWithoutDetail} cuenta(s) suspendida(s) sin «suspension» y ${detailWithoutSuspension} con «suspension» sin estar suspendidas; revisar.`);
}

print(`Campo usuarios.suspension documentado en «${suspensionDbName}» (sin índices nuevos)`);
