# Borde de distribución: Azure Front Door Standard (REQUIERE AUTORIZACIÓN)

> **Estado: preparado, NO aplicado.** Nada de esto existe en Azure hasta que una persona con permisos corra `terraform apply`. Hasta confirmarlo, ningún documento debe decir que la CDN está implementada.

Este directorio tiene su propio estado remoto (`edge.tfstate`), igual que `infra/`, `database/` y `apps/`: se puede destruir sin tocar lo demás. El módulo reutilizable está en `deploy/modules/edge`. Ninguna parte del CI lo despliega.

## Qué crea
Un perfil de **Front Door Standard**, un endpoint y tres rutas (las dos últimas son opcionales):
| Ruta | Origen | Caché |
|---|---|---|
| `/api/*`, `/health`, `/health/*` | el gateway (FQDN de su Container App, HTTPS con validación del certificado) | ninguna: todo pasa al origen |
| `static_path_pattern` (por omisión `/*`) | `static_host`: el contenido estático del panel y de la landing | sí, con compresión |
| `/imagenes-publicas/*` | `public_images_host`: la cuenta pública de imágenes (ADR-007) | sí; los nombres llevan el hash, son inmutables |

Las fotos de verificación y todo contenido privado **no pasan** por el borde (ADR-007, punto 4): viven en otra cuenta, con enlaces firmados de 5 minutos.

## Requisitos previos (los hace una persona; no los hace el código)
1. Registrar el proveedor: `az provider register --namespace Microsoft.Cdn` (ADR-006 lo dejó pendiente hasta decidir usar CDN).
2. Que existan el grupo de recursos y el estado remoto (`bootstrap/bootstrap.sh` ya los crea) y que `apps/` esté desplegado (de ahí sale la dirección del gateway; si no, pasar `-var gateway_host=<fqdn>`).
3. Decidir el origen estático del panel y la landing (`static_host`); si todavía no existe, se deja vacío y no se crea esa ruta.

## Cómo se prueba sin tocar Azure
```
cd deploy/edge
terraform init -backend=false
terraform validate
cd ../modules/edge
terraform init -backend=false
terraform test        # proveedor simulado: 3 pruebas, no crea nada
```
**Plan de solo lectura contra Azure: no se pudo preparar aquí.** `terraform plan` necesita la sesión de Azure de quien despliega y el estado remoto real; el agente no tiene ninguna. Quien aplique debe correr primero:
```
terraform init -backend-config=backend.hcl
terraform plan -var subscription_id=<ID> -var resource_group_name=<RG> -var name_suffix=<sufijo> [-var static_host=<host>] [-var public_images_host=<host>]
```
El plan debe mostrar **solo creaciones** (con ambos opcionales vacíos: perfil, endpoint, grupo de orígenes, origen y ruta del gateway = 5 recursos; con ambos, 11). Si aparece algún reemplazo o destrucción, no aplicar.

## Costo estimado (cifras del ADR-007, consultadas el 2026-11-06: **verificarlas antes del apply**)
| Concepto | Tarifa | En la ventana de evaluación (25-nov a 5-dic, 11 días) |
|---|---|---|
| Cuota base Standard (cobrada por hora de uso) | 35 USD al mes | unos **12.8 USD** (35 × 11 ÷ 30) |
| Solicitudes | 0.009 USD por cada 10 000 (Norteamérica y Europa) | centavos con el tráfico del piloto |
| Salida de datos | 0.083 USD por GB (Norteamérica y Europa, primeros 10 TB) | depende del tráfico; la salida de Azure a internet ya incluye 100 GB gratis al mes en el origen |
| Premium (no se usa) | 330 USD al mes | descartado por costo |

Sin tráfico igual se cobra la cuota base. Sale del crédito de Azure for Students (ADR-006). Standard **no incluye las reglas administradas del WAF**; solo reglas personalizadas.

## Cómo destruirlo
```
cd deploy/edge
terraform init -backend-config=backend.hcl
terraform destroy -var subscription_id=<ID> -var resource_group_name=<RG> -var name_suffix=<sufijo>
```
Solo borra el perfil de Front Door y sus rutas (estado `edge.tfstate`); no toca el gateway, las imágenes ni la base. La cuota base se deja de cobrar al borrarse el perfil. Para volver al estado anterior no hace falta cambiar código: el cliente seguía usando la dirección directa del gateway y la dirección base de imágenes es configurable (ADR-007).

## Riesgos que hay que resolver ANTES de mandar tráfico real por el borde
1. **IP del cliente y límite de peticiones.** El gateway solo confía en `X-Forwarded-For` de la red de la Container Apps (`ForwardedHeaders__KnownNetworks`). Detrás de Front Door la petición llega desde las IP del borde: sin cambiar esa configuración, los límites por IP (specs 006 y 009) verían a todos como pocas IP. Hay que decidir cómo confiar en el borde (rangos de la etiqueta de servicio `AzureFrontDoor.Backend` o la cabecera `X-Azure-ClientIP`). **No está implementado.**
2. **El gateway sigue siendo público.** Para que solo se llegue por el borde hay que exigir la cabecera `X-Azure-FDID` (salida `frontdoor_id`) o restringir el acceso. **No está implementado.**
3. **Orígenes permitidos.** `Cors__Panel__Origen` (variable `panel_origin` de `apps/`) y los hosts de foto (`Perfil__HostsFoto__*`) deben incluir el dominio nuevo si el panel o las imágenes se sirven desde él.
4. **Dominio propio y certificado.** Esta rama usa el dominio por omisión `*.azurefd.net`. Un dominio propio es otro paso.

Ver [ADR-007](../../docs/decisiones/ADR-007-cdn-de-imagenes.md).
