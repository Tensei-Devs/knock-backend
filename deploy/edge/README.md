# Borde de distribución: Azure Front Door Standard (REQUIERE AUTORIZACIÓN)

> **Estado: Módulo preparado y validado (terraform validate, 3 de 3 pruebas con proveedor simulado, plan real con 8 recursos a crear y 0 a destruir); no desplegado porque la suscripción Azure for Students prohíbe Front Door (error 400, 2026-11-21).** Mensaje de Azure: `400 BadRequest: Free Trial and Student account is forbidden for Azure Frontdoor resources`, al crear `module.edge.azurerm_cdn_frontdoor_profile.this` con `terraform apply` el 2026-11-21 (suscripción Azure for Students). Después del fallo, `terraform state list` en `deploy/edge` salió vacío y `az resource list -g rg-knock-dev` no mostró ningún recurso `afd`: no quedó nada creado.

Este directorio tiene su propio estado remoto (`edge.tfstate`), igual que `infra/`, `database/` y `apps/`: se puede destruir sin tocar lo demás. El módulo reutilizable está en `deploy/modules/edge`. Ninguna parte del CI lo despliega.

## Qué crea
Un perfil de **Front Door Standard**, un endpoint y tres rutas (las dos últimas son opcionales):
| Ruta | Origen | Caché |
|---|---|---|
| `/api/*`, `/health`, `/health/*` | el gateway (FQDN de su Container App, HTTPS con validación del certificado) | ninguna: todo pasa al origen |
| `static_path_pattern` (por omisión `/*`) | `static_host`: el contenido estático del panel y de la landing | sí, con compresión |
| `/imagenes-publicas/*` | `public_images_host`: la cuenta pública de imágenes (ADR-007) | sí; los nombres llevan el hash, son inmutables |

Las fotos de verificación y todo contenido privado **no pasan** por el borde (ADR-007, punto 4): viven en otra cuenta, con enlaces firmados de 5 minutos.

## Valores que fija quien aplica (no están en el repositorio)
| Valor | Qué es | De dónde sale |
|---|---|---|
| `<ID>` | ID de la suscripción de Azure | `az account show --query id -o tsv` (es el mismo de la variable `AZURE_SUBSCRIPTION_ID` del repositorio) |
| `<RG>` | grupo de recursos donde vive la infraestructura | el que usó `up.sh` (por omisión `rg-knock-dev`); Terraform **no** lo crea |
| `<sufijo>` | `name_suffix` (2 a 8 letras o números) | el mismo con que se creó `infra/` |
| `<host-estático>` | origen del contenido estático del panel y la landing, sin `https://` | por ejemplo el endpoint web de una cuenta de almacenamiento; si todavía no existe, **omitir `static_host`** y no se crea esa ruta |
| `<host-imágenes>` | host del almacenamiento público de imágenes, sin `https://` | sale de `public_images_base_url` de `infra/` (`terraform output public_images_base_url` en `deploy/infra`); es opcional |

## Pasos para aplicar (en orden, copiables; los corre una persona con su sesión de Azure)
> No corras esto hasta tener **el primer despliegue exitoso y la evidencia**. El tráfico real seguirá entrando por el gateway.

**1. Sesión y suscripción**
```
az login
az account set --subscription "<ID>"
az account show --query "{suscripcion:name, id:id, usuario:user.name}" -o table
```
**2. Registrar el proveedor de CDN** (una sola vez por suscripción; ADR-006 lo dejó pendiente)
```
az provider register --namespace Microsoft.Cdn
az provider show --namespace Microsoft.Cdn --query registrationState -o tsv     # espera «Registered» (unos minutos)
```
**3. Permisos que necesitas**
- Para registrar el proveedor: rol **Contributor** u **Owner** en la suscripción (permiso `Microsoft.Cdn/register/action`). Si ya lo registró alguien, no hace falta.
- Para crear y borrar Front Door: **Contributor** sobre el grupo de recursos `<RG>`.
- Para el estado remoto (`edge.tfstate` y la lectura de `apps.tfstate`): **Storage Blob Data Contributor** sobre la cuenta `stknocktfstate` (el backend usa Microsoft Entra, `use_azuread_auth = true`).
- Comprobación: `az role assignment list --assignee "$(az ad signed-in-user show --query id -o tsv)" --all -o table`.

**4. Terraform init con el backend y la clave de `edge.tfstate`** (la clave ya viene en `backend.hcl`: `edge.tfstate`)
```
cd deploy/edge
terraform init -backend-config=backend.hcl
```
**5. Plan (solo lectura)**
```
terraform plan -out edge.tfplan -var subscription_id=<ID> -var resource_group_name=<RG> -var name_suffix=<sufijo> \
  [-var static_host=<host-estático>] [-var public_images_host=<host-imágenes>]
```
Debe mostrar **solo creaciones** y terminar con `Plan: 5 to add, 0 to change, 0 to destroy.` (sin los dos opcionales; con ambos, `Plan: 11 to add`). Los recursos son: `azurerm_cdn_frontdoor_profile.this`, `…_endpoint.this`, `…_origin_group.gateway`, `…_origin.gateway` y `…_route.gateway` (más los de `static` e `images` si los pasaste). Si aparece algún `change`, `replace` o `destroy`, **no apliques**. Si falla al leer `apps.tfstate`, pasa `-var gateway_host=<fqdn del gateway sin https://>`.

**6. Apply**
```
terraform apply edge.tfplan
```
**7. Verificar** (la propagación del borde tarda unos 10 a 20 minutos)
```
terraform output edge_url
curl -sS -o /dev/null -w "%{http_code}\n" "$(terraform output -raw edge_url)/health"        # 200: llega al gateway
curl -sS -o /dev/null -w "%{http_code}\n" "$(terraform output -raw edge_url)/api/v1/oficios"  # igual que por la dirección directa del gateway
az afd profile show --resource-group <RG> --profile-name "afd-knock-dev-<sufijo>" --query "{estado:provisioningState,sku:sku.name}" -o table
```
Compara con la dirección directa (`gateway_url` de `apps/`): las respuestas deben ser iguales. La salida `frontdoor_id` es el valor de `X-Azure-FDID` (mejora documentada, no requisito). Guarda la captura como evidencia.

**8. Destruir** (al terminar la demostración; la cuota base se deja de cobrar al borrarse el perfil)
```
cd deploy/edge
terraform init -backend-config=backend.hcl
terraform destroy -var subscription_id=<ID> -var resource_group_name=<RG> -var name_suffix=<sufijo>
```
Solo borra lo de `edge.tfstate` (perfil, endpoint, orígenes y rutas de Front Door); no toca el gateway, las imágenes ni la base. Después `az afd profile list --resource-group <RG> -o table` debe salir vacío. No hay que cambiar código: nada apunta al borde.

## Validación sin Azure (ya hecha por el agente)
```
cd deploy/edge
terraform init -backend=false
terraform validate
cd ../modules/edge
terraform init -backend=false
terraform test        # proveedor simulado: 3 pruebas, no crea nada
```
El agente **no** pudo correr un `plan` contra Azure: necesita tu sesión y el estado remoto real. El paso 5 es el primer plan real.

## Costo estimado para unos pocos días (cifras del ADR-007, consultadas el 2026-11-06: **verificarlas antes del apply**)
| Concepto | Tarifa | Unos pocos días |
|---|---|---|
| Cuota base Standard (cobrada por hora de uso) | 35 USD al mes (**unos 1.2 USD al día**) | unos **3.5 USD por 3 días**, unos **5.8 USD por 5 días** |
| Solicitudes | 0.009 USD por cada 10 000 (Norteamérica y Europa) | centavos con tráfico de demostración |
| Salida de datos | 0.083 USD por GB (Norteamérica y Europa, primeros 10 TB) | centavos con tráfico de demostración |
| Premium (no se usa) | 330 USD al mes | descartado por costo |

Sin tráfico igual se cobra la cuota base, por eso se aplica solo después de la evidencia y se destruye al terminar. Sale del crédito de Azure for Students (ADR-006). Standard **no incluye las reglas administradas del WAF**; solo reglas personalizadas.

## Mejoras documentadas (no bloquean el apply ni la demostración)
Solo harían falta si algún día el tráfico real se enruta por el borde; mientras el gateway siga como entrada principal no aplican:
1. **Exigir `X-Azure-FDID` en el gateway** (salida `frontdoor_id`) para que solo atienda lo que pasó por Front Door. **No está implementado ni es requisito.**
2. **IP del cliente y límite de peticiones.** El gateway confía en `X-Forwarded-For` solo de la red de la Container Apps (`ForwardedHeaders__KnownNetworks`). Con tráfico real por el borde habría que confiar en `AzureFrontDoor.Backend` o usar `X-Azure-ClientIP`, o los límites por IP (specs 006 y 009) verían pocas IP. **No está implementado.**
3. **Orígenes permitidos.** `Cors__Panel__Origen` y `Perfil__HostsFoto__*` deberían incluir el dominio del borde si el panel o las imágenes se sirven desde él.
4. **Dominio propio y certificado.** Esta rama usa el dominio por omisión `*.azurefd.net`.

Ver [ADR-007](../../docs/decisiones/ADR-007-cdn-de-imagenes.md).
