# Content Safety para la moderación (spec 031, T-10) — PARCHE PREPARADO, NO EJECUTADO

REQUIERE AUTORIZACIÓN: toca `deploy/` y decide gasto en Azure. Nadie debe ejecutarlo sin que el líder apruebe el gasto (spec 031, §15).

## Qué crea
- Una cuenta de Azure AI Content Safety (`kind = "ContentSafety"`, SKU `F0` si la suscripción lo permite; si no, decisión del líder).
- Identidad administrada de Mercado con el rol «Cognitive Services User» sobre esa cuenta (rol mínimo para analizar).

## Variables de la aplicación (sin secretos)
| Variable | Valor |
|---|---|
| `Moderacion__Habilitado` | `true` solo cuando el líder encienda el gasto; por omisión `false` |
| `Moderacion__Modo` | `azure` |
| `Moderacion__ContentSafety__Endpoint` | salida `endpoint` del recurso (https) |
| `Moderacion__Limites__LlamadasPorMes` | por debajo de la cuota del plan gratuito confirmada en el portal |
| `Moderacion__VersionApi` | versión estable vigente; registrar en `Memoria.md` |

La clave no se usa: el servicio pide el token con la identidad administrada. Falta registrar un `TokenCredential` (paquete `Azure.Identity`, MIT) en el despliegue; sin credencial el servicio falla cerrado (503 `moderation_unavailable`).

## Fragmento de Terraform (referencia, sin aplicar)
```hcl
resource "azurerm_cognitive_account" "content_safety" {
  name                  = "${var.prefix}-content-safety"
  location              = var.location
  resource_group_name   = var.resource_group_name
  kind                  = "ContentSafety"
  sku_name              = "F0"
  custom_subdomain_name = "${var.prefix}-content-safety"
  local_auth_enabled    = false
}

resource "azurerm_role_assignment" "market_content_safety" {
  scope                = azurerm_cognitive_account.content_safety.id
  role_definition_name = "Cognitive Services User"
  principal_id         = var.market_identity_principal_id
}
```

## Procedimiento (lo ejecuta el líder)
1. Aprobar el gasto y el SKU. 2. Revisar y aplicar el fragmento en `deploy/`. 3. Registrar `Azure.Identity` y el `TokenCredential` en Mercado. 4. Poner las variables y `Moderacion__Habilitado=true`. 5. Prueba manual con texto e imagen sintéticos (una llamada por tipo) y anotarla en el PR. 6. Anotar versión de la API y de `Azure.Identity` en `Memoria.md`.
