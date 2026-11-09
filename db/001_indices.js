// 001 — Colecciones e índices declarados (spec 003: R-05, R-06, CA-01). Idempotente: crea lo que falta y no cambia lo que ya está.
//
// Uso (mongosh, sin la URI en la línea de comandos):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/001_indices.js
// Variables de entorno:
//   KNOCK_DB_URI         cadena de conexión; en Azure, desde Key Vault. Obligatoria. Nunca se imprime.
//   KNOCK_DB_MARKET      base del servicio de Mercado (por defecto «market», la de Mongo:Databases:Catalog).
//   KNOCK_DB_IDENTITY    base del servicio de Identidad (por defecto «identity»).
//   KNOCK_DB_MESSAGING   base del servicio de Mensajería (por defecto «messaging»).
// Los scripts de db/ se aplican en orden por su número.

const env = process.env;
if (!env.KNOCK_DB_URI) {
  print('Define KNOCK_DB_URI; la URI no se acepta como argumento.');
  quit(2);
}

const validName = /^[A-Za-z0-9_-]{1,63}$/;
const databases = {
  market: env.KNOCK_DB_MARKET || 'market',
  identity: env.KNOCK_DB_IDENTITY || 'identity',
  messaging: env.KNOCK_DB_MESSAGING || 'messaging',
};
for (const name of Object.values(databases)) {
  if (!validName.test(name)) {
    print("Nombre de base no válido (letras, dígitos, '_' o '-').");
    quit(2);
  }
}

// Colecciones técnicas de cada servicio: los pendientes se leen por estado y en orden de fecha (UTC).
const technical = {
  outbox: [{ key: { estado: 1, creadoEn: 1 } }],
  tareas_programadas: [{ key: { estado: 1, programadaPara: 1 } }],
};

// Catálogo de Mercado: los mismos índices que crea CatalogStore.EnsureIndexesAsync (la prueba CA-01 compara ambos).
const catalog = {
  colonias: [
    { key: { geom: '2dsphere' } },
    { key: { centro: '2dsphere' } },
    { key: { cp: 1 } },
    { key: { nombreBusqueda: 1 } },
    { key: { municipio: 1, cp: 1, nombre: 1 }, options: { unique: true } },
  ],
  oficios: [
    { key: { clave: 1 }, options: { unique: true } },
    { key: { nombreBusqueda: 1 } },
  ],
};

const plan = [
  [databases.market, Object.assign({}, technical, catalog)],
  [databases.identity, technical],
  [databases.messaging, technical],
];

const connection = new Mongo(env.KNOCK_DB_URI);
for (const [databaseName, collections] of plan) {
  const db = connection.getDB(databaseName);
  const existing = db.getCollectionNames();
  for (const [collection, indexes] of Object.entries(collections)) {
    if (!existing.includes(collection)) {
      db.createCollection(collection);
    }

    for (const index of indexes) {
      // Mismo nombre que da el driver por defecto (campo_tipo): reaplicar con la misma definición no cambia nada.
      db.getCollection(collection).createIndex(index.key, index.options || {});
    }
  }

  print(`${databaseName}: ${Object.keys(collections).join(', ')}`);
}
