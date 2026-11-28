# Terraform listo para los encendidos de Azure

## Qué cambió
- Las cuatro apps .NET reciben `AZURE_CLIENT_ID` con su identidad asignada por el usuario; Redis no cambia.
- El gateway recibe `Security__Hsts__SendInDevelopment=true` solo cuando el entorno es `Development` (BUG-002, enmienda 2026-11-27; el código llega aparte).
- El push queda detrás de `enable_push`, apagado por omisión.
- Content Safety y Azure OpenAI ya no usan llave propia; EXC-034 vence el 2026-12-25.

## Por qué
- Sin `AZURE_CLIENT_ID`, `DefaultAzureCredential` intentaría la identidad de sistema, que estas apps no tienen.
- La llave propia exige protección contra purga en el Key Vault, que es irreversible. Decisión del responsable del 2026-11-27.
- Redis queda fuera porque reiniciarlo pierde la lista de sesiones canceladas.

## A evitar
- Cambiar variables a mano en las apps: el siguiente `apply` las revierte.
- Pasar `enable_*` solo por línea de comandos y olvidarlo: `up.sh` no las pasa y planearía quitar lo encendido.

## Pendiente
- No se ejecutó `plan` ni `apply`, ni se probó en Azure. El `plan` mostrará cambios en las cuatro apps.
