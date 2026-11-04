# Despliegue en Azure con Terraform (spec 005)

Todo se crea y se borra con comandos. Hay tres directorios, cada uno con su propio estado remoto, para que borrar
una parte no toque las demás:

| Directorio | Qué contiene | Se destruye |
|---|---|---|
| `database/` | Clúster gratuito de Azure DocumentDB (solo se importa) | **Nunca** (`prevent_destroy`, CA-03) |
| `infra/` | Red, entorno de Container Apps, Key Vault, Blob Storage, ACR, logs (CA-01) | Paso 4 |
| `apps/` | Gateway, Identity, Messaging, Market y Redis | Paso 3 |

`modules/shared` es el módulo compartido; `modules/service` es un solo módulo de servicio que se instancia una vez
por servicio, cada una con su identidad administrada y acceso solo a sus secretos (R-05).

## Requisitos
Terraform >= 1.9, Azure CLI con `az login`, y la región `westus` (las otras permitidas por la suscripción:
`chilecentral`, `norwayeast`, `francecentral`, `northcentralus`; ADR-006).

## Paso 0 — una sola vez
`bootstrap/bootstrap.sh` crea el almacenamiento del estado y la identidad federada del pipeline. Lo corre una
persona; no guarda contraseñas. Luego, en cada directorio: `terraform init -backend-config=backend.hcl`.

## Paso 1 — crear
```
cd deploy/infra
terraform apply -var subscription_id=<ID> -var resource_group_name=<RG> -var name_suffix=<2-8 letras/números> \
  -var 'secret_admin_object_ids=["<tu object id>"]'
```
Con `database/` se importa el clúster la primera vez: `terraform apply -var subscription_id=... -var resource_group_name=... -var cluster_name=...`.

## Secretos (una persona, a mano)
Los valores no pasan por Terraform ni por el repositorio (CA-04). Nombres fijos en Key Vault:
`jwt-signing-key`, `redis-password`, `pii-encryption-key`, `pii-hmac-key`, `mongo-identity`, `mongo-market`,
`mongo-messaging`.
```
az keyvault secret set --vault-name <kv> --name redis-password --value "<valor>"
```

## Paso 2 — desplegar
Las imágenes deben estar en el ACR (las publica el pipeline, spec 007).
```
cd deploy/apps
terraform apply -var-file=envs/dev.tfvars -var subscription_id=<ID> -var resource_group_name=<RG> -var image_tag=<sha>
```
Para la ventana de evaluación (25-nov al 5-dic) usar `envs/evaluacion.tfvars`. Si el primer apply falla por permisos
sobre un secreto, espera unos minutos a que se propague el rol y repite.

## Paso 3 — eliminar lo desplegado
`cd deploy/apps && terraform destroy ...` (mismas variables). Deja la infraestructura y la base.

## Paso 4 — destruir
`cd deploy/infra && terraform destroy ...`. **No toques `database/`**: el clúster gratuito es único por suscripción.

## Comprobaciones sin Azure
```
terraform fmt -check -recursive deploy
for d in infra apps database; do (cd deploy/$d && terraform init -backend=false && terraform validate); done
(cd deploy/modules/shared && terraform init -backend=false && terraform test)
(cd deploy/modules/service && terraform init -backend=false && terraform test)
```
