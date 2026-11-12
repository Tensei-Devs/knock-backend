// Spec 020 (KNOCK-59): colecciones `solicitudes` y `cotizaciones` del módulo Requests de Mercado y sus índices.
// Uso (igual que 001_indices.js y 002_…, y como lo aplica db/entrega.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/020-requests-solicitudes-cotizaciones.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// La base se toma de KNOCK_DB_MARKET (por defecto «market», la de Mongo:Databases:Requests). Es idempotente: crea lo que falta y nunca borra ni
// cambia colecciones, índices ni documentos existentes. Las definiciones deben coincidir con las de RequestsStore (spec 020, §6).
const requestsDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(requestsDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const requestsMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(requestsDbName) : db.getSiblingDB(requestsDbName);

for (const name of ["solicitudes", "cotizaciones"]) {
  if (!requestsMarket.getCollectionNames().includes(name)) {
    requestsMarket.createCollection(name);
  }
}

// solicitudes: las del cliente (más recientes primero), el barrido de vencimiento por estado y fecha, y la búsqueda de las abiertas por oficio.
requestsMarket.solicitudes.createIndex({ clienteId: 1, creadoEn: -1 }, { name: "clienteId_1_creadoEn_-1" });
requestsMarket.solicitudes.createIndex({ estado: 1, venceEn: 1 }, { name: "estado_1_venceEn_1" });
requestsMarket.solicitudes.createIndex({ oficioClave: 1, estado: 1 }, { name: "oficioClave_1_estado_1" });

// cotizaciones: una por técnico en cada solicitud (R-17: la segunda choca con este índice único y responde 409); el conteo de cotizaciones vivas
// (R-20) y las cotizaciones de un técnico, más recientes primero.
requestsMarket.cotizaciones.createIndex({ solicitudId: 1, tecnicoId: 1 }, { unique: true, name: "solicitudId_1_tecnicoId_1" });
requestsMarket.cotizaciones.createIndex({ solicitudId: 1, estado: 1, creadoEn: 1 }, { name: "solicitudId_1_estado_1_creadoEn_1" });
requestsMarket.cotizaciones.createIndex({ tecnicoUsuarioId: 1, creadoEn: -1 }, { name: "tecnicoUsuarioId_1_creadoEn_-1" });

print(`Índices de solicitudes y cotizaciones listos en «${requestsDbName}»`);
