// Spec 052: bitácora del técnico (una por solicitud y técnico; se borra 30 días después del cierre de la solicitud). Idempotente.
const assistantLogDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(assistantLogDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}
const assistantLogMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(assistantLogDbName) : db.getSiblingDB(assistantLogDbName);
if (!assistantLogMarket.getCollectionNames().includes("bitacoras")) {
  assistantLogMarket.createCollection("bitacoras");
}
assistantLogMarket.bitacoras.createIndex({ solicitudId: 1, tecnicoId: 1 }, { name: "solicitudId_1_tecnicoId_1", unique: true });
// venceEn es null mientras la solicitud siga abierta; solo los documentos con fecha vencen.
assistantLogMarket.bitacoras.createIndex({ venceEn: 1 }, { name: "venceEn_1_ttl", expireAfterSeconds: 0, partialFilterExpression: { venceEn: { $type: "date" } } });
print(`Índices de bitácoras listos en «${assistantLogDbName}»`);
