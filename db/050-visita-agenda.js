// Spec 050 (KNOCK-266): índices de `visitas` para la agenda del técnico y las visitas del cliente. Los campos nuevos de la visita (`reprogramaciones`,
// `propuestaReprogramacion`, `tardia`, `quienFalto`, `cancelacion`) son aditivos y no necesitan índice. La colección y sus primeros índices los crea db/022-visitas.js.
// Uso (igual que 022-visitas.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/050-visita-agenda.js
// Es idempotente: crear un índice que ya existe no cambia nada.
const scheduleDbName = process.env.KNOCK_DB_MARKET || "market";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(scheduleDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const scheduleMarket = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(scheduleDbName) : db.getSiblingDB(scheduleDbName);
if (!scheduleMarket.getCollectionNames().includes("visitas")) {
  scheduleMarket.createCollection("visitas");
}

// Agenda del técnico: sus visitas entre dos días, ordenadas por día y hora (R-24).
scheduleMarket.visitas.createIndex({ tecnicoUsuarioId: 1, dia: 1, hora: 1 }, { name: "tecnicoUsuarioId_1_dia_1_hora_1" });
// Visitas y citas del cliente por estado y día (§6).
scheduleMarket.visitas.createIndex({ clienteId: 1, estado: 1, dia: 1 }, { name: "clienteId_1_estado_1_dia_1" });

print(`Índices de agenda de visitas listos en «${scheduleDbName}»`);
