// Spec 047 (KNOCK-57, T-01): colección `servicios_tecnico` e índices de §6.
// KNOCK_DB_URI=... mongosh --nodb --quiet --file db/047-servicios-tecnico.js
const servicesDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(servicesDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const servicesMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(servicesDbName) : db.getSiblingDB(servicesDbName);
if (!servicesMarket.getCollectionNames().includes("servicios_tecnico")) {
  servicesMarket.createCollection("servicios_tecnico");
}

// SRV-01 y filtros por oficio: servicios de un técnico.
servicesMarket.servicios_tecnico.createIndex({ tecnicoId: 1, oficio: 1 }, { name: "tecnicoId_1_oficio_1" });
// Autocompletado de servicios (texto en español).
servicesMarket.servicios_tecnico.createIndex({ nombre: "text" }, { name: "nombre_text", default_language: "spanish" });
print(`Índices de servicios_tecnico listos en «${servicesDbName}»`);
