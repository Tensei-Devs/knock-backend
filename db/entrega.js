// Script de entrega de la base de datos (spec 014, R-02 y CA-01): deja una instancia VACÍA lista para el sistema, en un solo paso:
//   1. aplica en orden los scripts numerados de db/ (001_indices.js, 002_… y los que se agreguen): colecciones e índices;
//   2. carga los datos de ejemplo del catálogo de Mercado (db/seeds/catalog/oficios.json y colonias.json).
// Es idempotente: volver a correrlo sobre una base ya cargada no duplica ni cambia nada (los datos se cargan con upsert por clave natural).
//
// Uso (mongosh, desde la raíz del repositorio y sin la URI en la línea de comandos):
//   KNOCK_DB_URI=... mongosh --nodb --quiet --file db/entrega.js
// Variables de entorno:
//   KNOCK_DB_URI      cadena de conexión; en Azure, desde Key Vault. Obligatoria. Nunca se imprime.
//   KNOCK_DB_MARKET   base del servicio de Mercado (por defecto «market»); las demás las toman los scripts numerados.
//   KNOCK_DB_DIR      carpeta db/ si el script no se corre desde la raíz del repositorio (por defecto «db»).
// Solo datos de ejemplo ficticios: nada de personas reales.

const fs = require('fs');

(function deliver() {
  const env = process.env;
  if (!env.KNOCK_DB_URI) {
    print('Define KNOCK_DB_URI; la URI no se acepta como argumento.');
    quit(2);
  }

  const dir = (env.KNOCK_DB_DIR || 'db').replace(/[\\/]+$/, '');
  const marketName = env.KNOCK_DB_MARKET || 'market';
  if (!/^[A-Za-z0-9_-]{1,63}$/.test(marketName)) {
    print("Nombre de base no válido (letras, dígitos, '_' o '-').");
    quit(2);
  }

  // 1. Scripts numerados, en orden, con load(): pasa por el evaluador de mongosh (que vuelve síncronas las llamadas del shell). Corren en el
  //    mismo ámbito global, así que sus nombres de nivel superior (const) no deben repetirse entre scripts numerados.
  const scripts = fs.readdirSync(dir).filter((name) => /^\d{3}[_-].+\.js$/.test(name)).sort();
  if (scripts.length === 0) {
    print(`No hay scripts numerados en ${dir}.`);
    quit(2);
  }

  for (const name of scripts) {
    print(`> ${name}`);
    load(`${dir}/${name}`);
  }

  // 2. Datos de ejemplo del catálogo (upsert por clave natural, con los _id fijos de las semillas).
  const read = (file) => EJSON.parse(fs.readFileSync(`${dir}/seeds/catalog/${file}`, 'utf8'));
  const market = new Mongo(env.KNOCK_DB_URI).getDB(marketName);

  const seed = (collectionName, documents, naturalKey) => {
    const collection = market.getCollection(collectionName);
    for (const document of documents) {
      const { _id, ...content } = document;
      const filter = {};
      for (const field of naturalKey) {
        filter[field] = document[field];
      }

      collection.updateOne(filter, { $set: content, $setOnInsert: { _id, version: 1 } }, { upsert: true });
    }

    print(`${marketName}.${collectionName}: ${collection.countDocuments({})} documentos`);
  };

  seed('oficios', read('oficios.json'), ['clave']);
  seed('colonias', read('colonias.json'), ['municipio', 'cp', 'nombre']);
  print('Entrega de la base lista.');
})();
