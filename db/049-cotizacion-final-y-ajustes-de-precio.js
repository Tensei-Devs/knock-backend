// Spec 049 (KNOCK-266): colección `ajustes_precio` y sus índices; la cotización final va embebida en `visitas` (campos aditivos, sin índice nuevo).
// Uso (igual que 020-requests-solicitudes-cotizaciones.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/049-cotizacion-final-y-ajustes-de-precio.js
// Es idempotente: crear una colección o un índice que ya existe no cambia nada.
const adjustmentsDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(adjustmentsDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const adjustmentsMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(adjustmentsDbName) : db.getSiblingDB(adjustmentsDbName);
if (!adjustmentsMarket.getCollectionNames().includes("ajustes_precio")) {
  adjustmentsMarket.createCollection("ajustes_precio");
}

// Ver si hay un ajuste pendiente en una visita (R-15) y encontrar los vencidos por la tarea VencerAjuste.
adjustmentsMarket.ajustes_precio.createIndex({ visitaId: 1, estado: 1 }, { name: "visitaId_1_estado_1" });
adjustmentsMarket.ajustes_precio.createIndex({ cotizacionId: 1, estado: 1 }, { name: "cotizacionId_1_estado_1" });

print(`Índices de ajustes_precio listos en «${adjustmentsDbName}»`);
