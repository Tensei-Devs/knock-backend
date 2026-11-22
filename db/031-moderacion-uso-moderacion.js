// Spec 031: registro de uso de la moderación, sin contenido, en `market` y `messaging`; vence a los 365 días. Idempotente.
const moderationDbNames = [process.env.KNOCK_DB_MARKET || "market", process.env.KNOCK_DB_MESSAGING || "messaging"];
for (const name of moderationDbNames) {
  if (!/^[A-Za-z0-9_-]{1,63}$/.test(name)) {
    print("Nombre de base no válido (letras, dígitos, '_' o '-').");
    quit(2);
  }
}
for (const name of moderationDbNames) {
  const target = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(name) : db.getSiblingDB(name);
  if (!target.getCollectionNames().includes("uso_moderacion")) {
    target.createCollection("uso_moderacion");
  }
  target.uso_moderacion.createIndex({ creadoEn: 1 }, { name: "creadoEn_1_ttl", expireAfterSeconds: 31536000 });
  target.uso_moderacion.createIndex({ contexto: 1, decision: 1, creadoEn: -1 }, { name: "contexto_1_decision_1_creadoEn_-1" });
  print(`Índices de uso de moderación listos en «${name}»`);
}
