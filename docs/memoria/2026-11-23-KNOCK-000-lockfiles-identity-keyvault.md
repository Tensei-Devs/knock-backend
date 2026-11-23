# 2026-11-23 · KNOCK-000 · Lockfiles de pruebas tras Azure.Identity y Key Vault de Identidad

- **Qué cambió:** se regeneraron `tests/ArchitectureTest`, `tests/DatabaseTest` y `tests/SecurityTest` `packages.lock.json` con `dotnet restore`; solo agregan `Azure.Security.KeyVault.Keys 4.8.0` (transitiva) y las dependencias `Azure.Identity` y `Azure.Core` de Knock.Identity.Api.
- **Por qué:** #305 agregó esos paquetes a Identidad y los proyectos de prueba que la referencian quedaron desactualizados; `dotnet restore --locked-mode` fallaba con NU1004 en el job API.
- **A evitar:** cambiar paquetes de un proyecto sin regenerar los lockfiles de todos los que lo referencian; verificar con `dotnet restore Knock.slnx --locked-mode`.
