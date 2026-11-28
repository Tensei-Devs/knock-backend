// Relleno de una sola vez para los técnicos guardados antes del 2026-11-25 (specs 027, 028, 046 y 051): escribe los campos de búsqueda y de
// reputación que el servicio ya escribe en las altas, ediciones y opiniones nuevas.
//
// NO es un script numerado a propósito: ni db/entrega.js ni el bucle de despliegue lo ejecutan. Es una migración de datos y la corre una persona.
//
//   Simulación (por omisión, no escribe nada):
//     KNOCK_DB_URI=... mongosh --nodb --quiet --file db/relleno/tecnicos-busqueda.js
//   Aplicar, solo después de leer la simulación:
//     KNOCK_DB_URI=... KNOCK_APLICAR=si mongosh --nodb --quiet --file db/relleno/tecnicos-busqueda.js
//
//   KNOCK_DB_URI     cadena de conexión; en Azure, desde Key Vault. Obligatoria. Nunca se imprime.
//   KNOCK_DB_MARKET  base de Mercado; por omisión «market».
//   KNOCK_APLICAR    solo el valor exacto «si» escribe.
//
// La salida solo lleva conteos, nombres de campos e identificadores de documento: nunca nombres de personas ni de negocios.
// Es idempotente: solo cambia lo que difiere, y cada escritura exige que los datos de origen sigan igual que al leerlos.
const fillEnv = process.env;
if (!fillEnv.KNOCK_DB_URI) {
  print('Define KNOCK_DB_URI; la URI no se acepta como argumento.');
  quit(2);
}
const fillDbName = fillEnv.KNOCK_DB_MARKET || 'market';
if (!/^[A-Za-z0-9_-]{1,63}$/.test(fillDbName)) {
  print('Nombre de base no válido.');
  quit(2);
}
const fillApply = fillEnv.KNOCK_APLICAR === 'si';
const fillMarket = new Mongo(fillEnv.KNOCK_DB_URI).getDB(fillDbName);

// Igual que SearchNameNormalizer.NormalizeQuery del servicio: minúsculas, sin acentos salvo la «ñ», y todo lo que no sea letra o dígito separa.
function fillWords(text) {
  let result = '';
  let separator = false;
  for (const original of text.normalize('NFC').split('')) {
    const lower = original.toLowerCase().charAt(0);
    const base = lower === 'ñ' ? lower : lower.normalize('NFD').charAt(0);
    if (!/[\p{L}\p{Nd}]/u.test(base)) {
      separator = true;
      continue;
    }
    if (separator && result.length > 0) result += ' ';
    separator = false;
    result += base;
  }
  return [...new Set(result.split(' ').filter((word) => word.length > 0))];
}

function fillNeighborhoodName(id) {
  if (typeof id !== 'string' || !/^[0-9a-fA-F]{24}$/.test(id)) return null;
  const found = fillMarket.colonias.findOne({ _id: new ObjectId(id) }, { nombre: 1 });
  return found && typeof found.nombre === 'string' ? found.nombre : null;
}

// Cambios que necesita un técnico. «guard» son los datos de origen que no deben haber cambiado al escribir.
function fillPlan(technician) {
  const set = {};
  const unset = {};
  const guard = {};
  const keep = (field) => {
    guard[field] = technician[field] === undefined ? { $exists: false } : technician[field];
  };

  if (typeof technician.nombre === 'string') {
    const words = fillWords(`${technician.nombre} ${technician.nombreNegocio || ''}`);
    if (technician.nombrePublico !== technician.nombre) set.nombrePublico = technician.nombre;
    if (JSON.stringify(technician.palabrasBusqueda) !== JSON.stringify(words)) set.palabrasBusqueda = words;
    keep('nombre');
    keep('nombreNegocio');
  }

  const reputation = technician.reputacion;
  if (reputation && reputation.total !== undefined && reputation.total !== null) {
    const total = Number(reputation.total);
    if (technician.totalResenas === undefined || Number(technician.totalResenas) !== total) set.totalResenas = new Int32(total);
    if (reputation.promedio === null || reputation.promedio === undefined) {
      if (technician.calificacion !== undefined) unset.calificacion = '';
    } else if (technician.calificacion === undefined || Number(technician.calificacion) !== Number(reputation.promedio)) {
      set.calificacion = new Double(Number(reputation.promedio));
    }
    guard['reputacion.total'] = reputation.total;
    guard['reputacion.promedio'] = reputation.promedio === undefined ? null : reputation.promedio;
  }

  const neighborhood = fillNeighborhoodName(technician.coloniaBaseId);
  if (neighborhood !== null && technician.coloniaBaseNombre !== neighborhood) {
    set.coloniaBaseNombre = neighborhood;
    keep('coloniaBaseId');
  }

  return { set, unset, guard, fields: [...Object.keys(set), ...Object.keys(unset).map((field) => `-${field}`)] };
}

const fillCounts = {};
let fillRead = 0;
let fillPending = 0;
let fillWritten = 0;
let fillSkipped = 0;

print(`Relleno de técnicos en «${fillDbName}»: ${fillApply ? 'APLICAR' : 'SIMULACIÓN (no escribe nada)'}`);
// Las cuentas eliminadas se excluyen: su nombre ya fue anonimizado y no deben volver a ser buscables.
// Con un bucle (y no «forEach»): mongosh no espera las llamadas a la base hechas dentro de una función pasada a un método del cursor.
const fillCursor = fillMarket.tecnicos.find({ estado: { $ne: 'eliminado' } }).sort({ _id: 1 });
while (fillCursor.hasNext()) {
  const technician = fillCursor.next();
  fillRead += 1;
  const plan = fillPlan(technician);
  if (plan.fields.length === 0) continue;
  fillPending += 1;
  for (const field of plan.fields) fillCounts[field] = (fillCounts[field] || 0) + 1;
  print(`  ${technician._id.toString()}: ${plan.fields.join(', ')}`);
  if (!fillApply) continue;

  const update = {};
  if (Object.keys(plan.set).length > 0) update.$set = plan.set;
  if (Object.keys(plan.unset).length > 0) update.$unset = plan.unset;
  const result = fillMarket.tecnicos.updateOne({ _id: technician._id, estado: { $ne: 'eliminado' }, ...plan.guard }, update);
  // mongosh puede devolver los contadores como Long: se comparan como texto (con «=== 1» las escrituras hechas salían como omitidas).
  if (String(result.modifiedCount) === '1') fillWritten += 1;
  else fillSkipped += 1;
}

print(`Técnicos revisados: ${fillRead}`);
print(`Técnicos con cambios pendientes: ${fillPending}`);
for (const field of Object.keys(fillCounts).sort()) print(`  ${field}: ${fillCounts[field]}`);
if (fillApply) {
  print(`Técnicos escritos: ${fillWritten}`);
  print(`Omitidos porque cambiaron mientras corría: ${fillSkipped} (vuelve a correr la simulación)`);
} else {
  print('Simulación terminada: no se escribió nada. Para aplicar, repite con KNOCK_APLICAR=si.');
}
