// Spec 051 (KNOCK-266, T-01): colección `opiniones` e índices de §6.
// KNOCK_DB_URI=... mongosh --nodb --quiet --file db/051-opiniones-y-reputacion.js
const reviewsDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(reviewsDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const reviewsMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(reviewsDbName) : db.getSiblingDB(reviewsDbName);
if (!reviewsMarket.getCollectionNames().includes("opiniones")) {
  reviewsMarket.createCollection("opiniones");
}

// Una opinión por visita: también decide la carrera entre dos envíos iguales (CA-28).
reviewsMarket.opiniones.createIndex({ visitaId: 1 }, { name: "visitaId_1", unique: true });
// OPI-01: opiniones de un técnico, de la más nueva a la más vieja.
reviewsMarket.opiniones.createIndex({ tecnicoId: 1, creadoEn: -1 }, { name: "tecnicoId_1_creadoEn_-1" });
print(`Índices de opiniones listos en «${reviewsDbName}»`);
