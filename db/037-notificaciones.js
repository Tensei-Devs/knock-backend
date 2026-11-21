// Spec 037 (KNOCK-60): colecciones `notifications.dispositivos`, `notifications.enviados` y `notifications.pendientes` del servicio de Mensajería y sus índices.
// Uso (igual que 030-conversaciones.js):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/037-notificaciones.js
// Con `mongosh "<cadena>" --file …` (sin KNOCK_DB_URI) usa la conexión abierta.
// La base se toma de KNOCK_DB_MESSAGING (por defecto «messaging»). Es idempotente: crea lo que falta y nunca borra ni cambia colecciones, índices ni documentos existentes.
// La definición debe coincidir con spec 037 (R-01, R-02, R-06, R-07, R-12 y R-13) y con los stores del módulo Avisos cuando existan.
const noticeDbName = process.env.KNOCK_DB_MESSAGING || "messaging";
if (!/^[A-Za-z0-9_-]{1,63}$/.test(noticeDbName)) {
  print("Nombre de base no válido (letras, dígitos, '_' o '-').");
  quit(2);
}

const noticeMessaging = process.env.KNOCK_DB_URI ? new Mongo(process.env.KNOCK_DB_URI).getDB(noticeDbName) : db.getSiblingDB(noticeDbName);

for (const collection of ["notifications.dispositivos", "notifications.enviados", "notifications.pendientes"]) {
  if (!noticeMessaging.getCollectionNames().includes(collection)) {
    noticeMessaging.createCollection(collection);
  }
}

// dispositivos: el token (guardado cifrado, buscado por su huella) es único y la última persona que lo registra gana (R-01); se borra por persona y por `sid` (R-02, R-13)
// y los registros sin renovar en 30 días vencen por TTL (R-02).
noticeMessaging.getCollection("notifications.dispositivos").createIndex({ tokenHuella: 1 }, { name: "tokenHuella_1", unique: true });
noticeMessaging.getCollection("notifications.dispositivos").createIndex({ personaId: 1, plataforma: 1 }, { name: "personaId_1_plataforma_1" });
noticeMessaging.getCollection("notifications.dispositivos").createIndex({ sid: 1 }, { name: "sid_1" });
noticeMessaging.getCollection("notifications.dispositivos").createIndex({ renovadoEn: 1 }, { expireAfterSeconds: 2592000, name: "renovadoEn_1" });

// enviados: un evento se envía una sola vez por persona (R-06) y el límite por persona y hora cuenta los envíos recientes (R-12); el registro vence a los 30 días.
noticeMessaging.getCollection("notifications.enviados").createIndex({ eventoId: 1, destinatario: 1 }, { name: "eventoId_1_destinatario_1", unique: true });
noticeMessaging.getCollection("notifications.enviados").createIndex({ destinatario: 1, creadoEn: -1 }, { name: "destinatario_1_creadoEn_-1" });
noticeMessaging.getCollection("notifications.enviados").createIndex({ creadoEn: 1 }, { expireAfterSeconds: 2592000, name: "creadoEn_1" });

// pendientes: la ventana de 5 minutos de `mensaje_nuevo` es única por conversación y persona (R-07); lo pendiente se lee por vencimiento y los restos vencen a las 24 horas.
noticeMessaging.getCollection("notifications.pendientes").createIndex({ conversacionId: 1, destinatario: 1 }, { name: "conversacionId_1_destinatario_1", unique: true });
noticeMessaging.getCollection("notifications.pendientes").createIndex({ cierraEn: 1 }, { name: "cierraEn_1" });
noticeMessaging.getCollection("notifications.pendientes").createIndex({ creadoEn: 1 }, { expireAfterSeconds: 86400, name: "creadoEn_1" });

print(`Índices de notifications.dispositivos, notifications.enviados y notifications.pendientes listos en «${noticeDbName}»`);
