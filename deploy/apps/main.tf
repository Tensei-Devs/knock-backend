# Pasos 2 y 3 «desplegar» y «eliminar lo desplegado» (spec 005): los servicios viven en su propio estado para
# poder borrarlos sin tocar la infraestructura compartida ni la base de datos.
#
# Los secretos (nombres fijados aquí, spec 005 §5) deben existir en Key Vault antes de aplicar; sus valores los
# escribe una persona con `az keyvault secret set` y nunca pasan por Terraform ni por el repositorio (CA-04).

data "terraform_remote_state" "infra" {
  backend = "azurerm"
  config = {
    resource_group_name  = "rg-knock-tfstate"
    storage_account_name = "stknocktfstate"
    container_name       = "tfstate"
    key                  = "infra.tfstate"
    use_azuread_auth     = true
    subscription_id      = var.subscription_id
    use_oidc             = var.use_oidc
  }
}

locals {
  infra = data.terraform_remote_state.infra.outputs
  tags  = { proyecto = "knock", entorno = var.environment }

  image = { for s in ["gateway", "identity", "messaging", "market"] :
    s => "${local.infra.registry_login_server}/knock-${s}:${var.image_tag}"
  }

  # Dirección interna de cada servicio dentro del entorno: la Container App se resuelve por su nombre.
  cluster_address = { for s in ["identity", "messaging", "market"] : s => "http://knock-${s}/" }

  redis_env = {
    Redis__Host = "knock-redis"
    Redis__Port = "6379"
  }
  # Nombres de los secretos en Key Vault (no son valores).
  kv           = { redis = "redis-password" }
  redis_secret = { Redis__Password = local.kv.redis }

  services = {
    gateway = {
      external = true
      env = {
        ReverseProxy__Clusters__identidad__Destinations__principal__Address  = local.cluster_address.identity
        ReverseProxy__Clusters__mercado__Destinations__principal__Address    = local.cluster_address.market
        ReverseProxy__Clusters__mensajeria__Destinations__principal__Address = local.cluster_address.messaging
      }
      secret_env = merge(local.redis_secret, { Jwt__SigningKey = "jwt-signing-key" })
    }
    identity = {
      external = false
      env      = local.redis_env
      secret_env = merge(local.redis_secret, {
        ConnectionStrings__KnockMongo = "mongo-identity"
        Jwt__SigningKey               = "jwt-signing-key"
        Pii__EncryptionKey            = "pii-encryption-key"
        Pii__HmacKey                  = "pii-hmac-key"
      })
    }
    messaging = {
      external   = false
      env        = {}
      secret_env = { ConnectionStrings__KnockMongo = "mongo-messaging" }
    }
    market = {
      external = false
      env      = local.redis_env
      secret_env = merge(local.redis_secret, {
        ConnectionStrings__KnockMongo = "mongo-market"
        Jwt__SigningKey               = "jwt-signing-key"
      })
    }
  }
}

module "redis" {
  source = "../modules/service"

  name                = "knock-redis"
  location            = var.location
  resource_group_name = var.resource_group_name
  environment_id      = local.infra.container_app_environment_id
  key_vault_id        = local.infra.key_vault_id
  key_vault_uri       = local.infra.key_vault_uri
  image               = var.redis_image
  secret_env          = { REDIS_PASSWORD = local.kv.redis }
  ingress_transport   = "tcp"
  target_port         = 6379
  health_path         = null
  min_replicas        = 1
  max_replicas        = 1
  command             = ["sh", "-c"]
  # Sin persistencia (spec 003, R-07): si Redis se reinicia, se pierde la lista de sesiones canceladas por `sid`
  # y un token cerrado vale hasta 15 minutos. Riesgo aceptado (constitución §12).
  args = ["exec redis-server --requirepass \"$REDIS_PASSWORD\" --save '' --appendonly no"]
  tags = local.tags
}

module "service" {
  source   = "../modules/service"
  for_each = local.services

  name                           = "knock-${each.key}"
  location                       = var.location
  resource_group_name            = var.resource_group_name
  environment_id                 = local.infra.container_app_environment_id
  registry_id                    = local.infra.registry_id
  registry_login_server          = local.infra.registry_login_server
  key_vault_id                   = local.infra.key_vault_id
  key_vault_uri                  = local.infra.key_vault_uri
  image                          = local.image[each.key]
  ingress_external               = each.value.external
  env                            = each.value.env
  secret_env                     = each.value.secret_env
  min_replicas                   = var.replicas[each.key].min
  max_replicas                   = var.replicas[each.key].max
  enable_app_insights            = true
  app_insights_connection_string = local.infra.application_insights_connection_string
  tags                           = local.tags
}
