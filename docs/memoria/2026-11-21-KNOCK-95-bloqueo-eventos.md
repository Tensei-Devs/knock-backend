# Eventos de bloqueo hacia la bandeja de salida (spec 043 T-08, parcial)

## Qué cambió
Identidad publica `BloqueoCreado` y `BloqueoEliminado` por `IBlockEvents` (contrato en `Knock.Contracts.Safety`).

## Por qué
R-23 y CA-33 (lado de Identidad).

## A evitar
Incluir nombres, teléfono o texto en el evento; aplicar el bloqueo leyendo la base de Identidad desde otro servicio.