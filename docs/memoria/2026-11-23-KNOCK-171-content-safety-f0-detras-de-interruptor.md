# KNOCK-171 — Content Safety F0 en Terraform y cableado de Market detrás de interruptores apagados (spec 031 R-15)

## Qué cambió
- `deploy/infra`: recurso `azurerm_cognitive_account` de tipo `ContentSafety` en el SKU gratuito F0 detrás de `enable_content_safety` (apagado), sin llaves (`local_auth_enabled=false`), con llave de cifrado propia en Key Vault como Azure OpenAI (CKV2_AZURE_22). Salidas `content_safety_id` y `content_safety_endpoint`.
- `deploy/apps`: `enable_content_safety` (apagado). Encendido, Market recibe `Moderacion__Habilitado=true` y `Moderacion__ContentSafety__Endpoint`, y su identidad administrada el rol `Cognitive Services User` solo sobre ese recurso.
- `AddModerationModule`: con la moderación habilitada registra un `TokenCredential` (`DefaultAzureCredential`, gana uno ya registrado) para que `ContentSafetyClient` pida el token con la identidad administrada; antes, sin credencial registrada, el cliente llamaba sin autenticar y todo caía en 503.

## Por qué
El cliente de Content Safety ya existía pero no tenía recurso, endpoint, rol ni credencial. Así queda todo listo y apagado, para encender tras el humo sin tocar código.

## A evitar
Encender `Moderacion__Habilitado` sin el recurso ni el rol (todo daría 503); poner valores distintos de `enable_content_safety` en `infra` y en `apps`; superar la cuota F0 (5 000 textos y 5 000 imágenes al mes; el tope local `Moderacion:Limites` ya la respeta). Verificar al encender que el SKU F0 admite la llave propia en la región; si no, quitar el CMK con una excepción de Checkov documentada.

## Dependencias
Ninguna nueva (`Azure.Identity` ya estaba en Market).
