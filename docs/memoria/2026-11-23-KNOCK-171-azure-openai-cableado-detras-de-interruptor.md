# KNOCK-171 — cableado de Azure OpenAI hacia Market detrás de interruptores apagados (spec 024 R-09 y R-15)

## Qué cambió
- `deploy/infra`: salidas `azure_openai_id`, `azure_openai_endpoint` y `azure_openai_deployment` de la cuenta ya declarada detrás de `enable_azure_openai` (vacías mientras esté apagado).
- `deploy/apps`: `enable_azure_openai` (apagado). Encendido, Market recibe `Asistente__Habilitado=true`, `Asistente__AzureOpenAI__Endpoint` y `Asistente__AzureOpenAI__ModeloDespliegue`, y su identidad administrada el rol `Cognitive Services OpenAI User` solo sobre esa cuenta.
- `AddAssistantModule`: con el asistente habilitado registra un `TokenCredential` (`DefaultAzureCredential`, gana uno ya registrado) para que `ProveedorIa` pida el token con la identidad administrada; antes, sin credencial registrada, llamaba sin autenticar y la cuenta (sin llaves) respondía 401.

## Por qué
La cuenta y el cliente existían pero faltaba el hilo entre ambos: endpoint, despliegue, rol y credencial. Queda todo listo y apagado, para encender tras el humo y la verificación de disponibilidad del modelo sin tocar código.

## A evitar
Encender `Asistente__Habilitado` sin la cuenta ni el rol; poner valores distintos de `enable_azure_openai` en `infra` y `apps`; subir `azure_openai_capacity_k_tpm` sin permiso de José (cuesta por token).

## Dependencias
Ninguna nueva (`Azure.Identity` ya estaba en Market).
