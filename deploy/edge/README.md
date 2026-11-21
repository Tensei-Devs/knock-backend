# Borde de distribución: Azure Front Door Standard (REQUIERE AUTORIZACIÓN)

> **Estado: preparado, NO aplicado.** Nada de esto existe en Azure hasta que una persona con permisos corra `terraform apply`, **después del primer despliegue exitoso y de la evidencia**. Hasta confirmarlo, ningún documento debe decir que la CDN está implementada. **El tráfico real no se enruta por Front Door: el gateway sigue siendo la entrada principal.**

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

## Costo estimado para unos pocos días (cifras del ADR-007, consultadas el 2026-11-06: **verificarlas antes del apply**)
| Concepto | Tarifa | Unos pocos días |
|---|---|---|
| Cuota base Standard (cobrada por hora de uso) | 35 USD al mes (**unos 1.2 USD al día**) | unos **3.5 USD por 3 días**, unos **5.8 USD por 5 días** |
| Solicitudes | 0.009 USD por cada 10 000 (Norteamérica y Europa) | centavos con tráfico de demostración |
| Salida de datos | 0.083 USD por GB (Norteamérica y Europa, primeros 10 TB) | centavos con tráfico de demostración |
| Premium (no se usa) | 330 USD al mes | descartado por costo |

Sin tráfico igual se cobra la cuota base, por eso se aplica solo después de la evidencia y se destruye al terminar. Sale del crédito de Azure for Students (ADR-006). Standard **no incluye las reglas administradas del WAF**; solo reglas personalizadas.

## Cómo destruirlo
```
cd deploy/edge
terraform init -backend-config=backend.hcl
terraform destroy -var subscription_id=<ID> -var resource_group_name=<RG> -var name_suffix=<sufijo>
```
Solo borra el perfil de Front Door y sus rutas (estado `edge.tfstate`); no toca el gateway, las imágenes ni la base. La cuota base se deja de cobrar al borrarse el perfil. Para volver al estado anterior no hace falta cambiar código: el cliente seguía usando la dirección directa del gateway y la dirección base de imágenes es configurable (ADR-007).

## Mejoras documentadas (no bloquean el apply ni la demostración)
Solo harían falta si algún día el tráfico real se enruta por el borde; mientras el gateway siga como entrada principal no aplican:
1. **Exigir `X-Azure-FDID` en el gateway** (salida `frontdoor_id`) para que solo atienda lo que pasó por Front Door. **No está implementado ni es requisito.**
2. **IP del cliente y límite de peticiones.** El gateway confía en `X-Forwarded-For` solo de la red de la Container Apps (`ForwardedHeaders__KnownNetworks`). Con tráfico real por el borde habría que confiar en `AzureFrontDoor.Backend` o usar `X-Azure-ClientIP`, o los límites por IP (specs 006 y 009) verían pocas IP. **No está implementado.**
3. **Orígenes permitidos.** `Cors__Panel__Origen` y `Perfil__HostsFoto__*` deberían incluir el dominio del borde si el panel o las imágenes se sirven desde él.
4. **Dominio propio y certificado.** Esta rama usa el dominio por omisión `*.azurefd.net`.

Ver [ADR-007](../../docs/decisiones/ADR-007-cdn-de-imagenes.md).
