// Spec 037 T-03: el identificador público opaco es distinto del ObjectId interno.
// Se aplica después de 037-notificaciones.js por orden lexicográfico en entrega.js.
const deviceIdDbName = process.env.KNOCK_DB_MESSAGING || "messaging";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(deviceIdDbName)) {
  print("Nombre de base no válido.");
  quit(2);
}
const deviceIdDb = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(deviceIdDbName) : db.getSiblingDB(deviceIdDbName);
deviceIdDb.getCollection("notifications.dispositivos").createIndex({ id: 1 }, { name: "id_1", unique: true });
