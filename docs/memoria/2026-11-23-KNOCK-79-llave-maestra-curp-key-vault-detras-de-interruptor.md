# KNOCK-79 — llave maestra de la CURP en Key Vault detrás de `Verificacion:KeyVault:Habilitado` (spec 025 R-11)

## Qué cambió
- `KeyVaultVerificationKeyWrapper`: implementa `IVerificationKeyWrapper` con `WrapKey`/`UnwrapKey` (RSA-OAEP-256) sobre la llave `curp-kek` de Key Vault y la identidad administrada del servicio (`Azure.Security.KeyVault.Keys` y `Azure.Identity`). Envuelve con la versión configurada y desenvuelve con la versión que trae lo guardado (`llaveKekVersion`), así una rotación no rompe verificaciones anteriores. Cualquier fallo del vault responde 503 `verification_storage_unavailable` sin contenido ni identificadores.
- `KeyVaultVerificationOptions` (`Verificacion:KeyVault`: `Habilitado`, `VaultUri`, `KeyName`, `KekVersion`) con validación al arrancar solo si está encendido.
- `AddKeyVaultVerificationKeyWrapper`: con el interruptor apagado (por omisión) sigue `UnavailableVerificationKeyWrapper` (falla cerrado) y no se crea ningún cliente de Azure; un doble registrado antes sigue ganando (pruebas).
- Terraform: `enable_curp_kek` en `deploy/infra` (llave `curp-kek`, apagado) y `enable_curp_keyvault` y `curp_kek_version` en `deploy/apps` (variables de entorno y rol `Key Vault Crypto Service Encryption User` sobre la llave, apagado).

## Por qué
La verificación real de identidad necesita cifrar las fotos con una llave de datos protegida por una llave maestra que nunca sale del vault. El código queda listo y apagado para encenderlo tras el humo sin tocar código.

## A evitar
Registrar llaves, CURP o el cuerpo de una respuesta del vault; encender el interruptor sin `KekVersion` o sin el rol; crear en Terraform una llave que ya existe sin importarla (crearía una versión nueva distinta de la `KekVersion` guardada); apuntar `KekVersion` a una versión deshabilitada.

## Dependencias
`Azure.Identity` 1.21.0 (la misma versión que Mercado) y `Azure.Security.KeyVault.Keys` 4.8.0, ambas de Microsoft y con licencia MIT.
