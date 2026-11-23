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

  # Datos de demostración (`Seed:Demo`): apagado salvo que se pida en el tfvars (ver deploy/README.md).
  seed_demo = var.seed_demo ? { Seed__Demo = "true" } : {}

  image = { for s in ["gateway", "identity", "messaging", "market"] :
    s => "${local.infra.registry_login_server}/knock-${s}:${var.image_tag}"
  }

  # Dirección interna de cada servicio dentro del entorno: la Container App se resuelve por su nombre.
  cluster_address = { for s in ["identity", "messaging", "market"] : s => "http://knock-${s}/" }

  # `redis` es la contraseña del servidor Redis; `redis_cs` es la cadena que leen los servicios
  # (ConnectionStrings:KnockRedis, p. ej. «knock-redis:6379,password=...»; spec 003, R-02).
  kv           = { redis = "redis-password", redis_cs = "redis-connection-string" }
  redis_secret = { ConnectionStrings__KnockRedis = local.kv.redis_cs }

  # Moderación con Content Safety (spec 031): solo con `enable_content_safety`; apagado no se define nada y Market falla cerrado como hoy. La credencial es la identidad administrada (rol abajo).
  content_safety_env = var.enable_content_safety ? {
    Moderacion__Habilitado              = "true"
    Moderacion__ContentSafety__Endpoint = local.infra.content_safety_endpoint
  } : {}

  # Imágenes públicas (spec 032, R-03): la dirección base sale de la infraestructura compartida; el host se pasa a las specs consumidoras (017: Perfil:HostsFoto).
  images_base_url = local.infra.public_images_base_url
  images_host     = split("/", local.infra.public_images_base_url)[2]

  # Red de confianza para X-Forwarded-For (gateway, Identity, Market y Messaging; spec 009 R-04 y spec 006 R-03).
  forwarded = { ForwardedHeaders__KnownNetworks__0 = var.container_apps_subnet_cidr }

  # Panel interno (spec 033, R-13 y R-27): el origen exacto (https, sin ruta) que el gateway e Identity aceptan para el panel.
  # Vacío = ningún origen permitido (el panel no abre desde un navegador), así que no se declara la variable de entorno.
  panel_origin = var.panel_origin == "" ? {} : { Cors__Panel__Origen = var.panel_origin }

  # Entorno de ASP.NET y diagnóstico (decisión de José: dev corre como Development; evaluación y producción como Production). El detalle de excepciones nunca sale en una respuesta.
  runtime = {
    ASPNETCORE_ENVIRONMENT  = var.aspnetcore_environment
    Errores__ExponerDetalle = "false"
  }

  services = {
    gateway = {
      external = true
      env = merge(local.runtime, local.forwarded, local.panel_origin, {
        ReverseProxy__Clusters__identidad__Destinations__principal__Address  = local.cluster_address.identity
        ReverseProxy__Clusters__mercado__Destinations__principal__Address    = local.cluster_address.market
        ReverseProxy__Clusters__mensajeria__Destinations__principal__Address = local.cluster_address.messaging
      })
      # La llave propia del panel (`PanelJwt:SigningKey`, Base64 de al menos 32 bytes) es distinta de `jwt-signing-key`: el gateway valida con ella los tokens de /api/v1/panel/**.
      secret_env = merge(local.redis_secret, {
        Jwt__SigningKey      = "jwt-signing-key"
        PanelJwt__SigningKey = "panel-jwt-signing-key"
      })
    }
    identity = {
      external = false
      # Identity lee la sección Identity:PhoneProtection (PhoneProtectionOptions): llaves numeradas y su versión vigente.
      # Firebase:ProjectId ya trae su valor en appsettings.json (knock-0).
      env = merge(local.runtime, local.forwarded, local.panel_origin, local.seed_demo, {
        Identity__PhoneProtection__EncryptionKeyVersion = "1"
        # Cifrado del secreto TOTP del panel (spec 033, R-26): la versión vigente no es secreta y la llave sí.
        PanelTotp__KeyVersion                     = "1"
        Identity__PhoneProtection__HmacKeyVersion = "1"
        # Cifrado de campos de Identity (spec 040, direcciones del cliente): la versión vigente no es secreta y la llave sí.
        Security__FieldEncryption__ActiveVersion = "1"
        # Dirección interna de Mercado: Identity valida la colonia del perfil con GET /api/v1/colonias/existe (spec 017, R-06). Sin esto, PUT /me con colonia da 503.
        Services__Market__BaseUrl = local.cluster_address.market
        # Host permitido de la foto de perfil (spec 017, R-05): el mismo de las imágenes públicas (spec 032, R-03).
        Perfil__HostsFoto__0 = local.images_host
        # HMAC de la CURP (spec 025, R-05): la versión vigente no es secreta y la llave sí.
        Verificacion__CurpHmac__ActiveVersion = "1"
        # Llave maestra de la verificación en Key Vault (spec 025 R-11): apagada por omisión; ninguno de estos valores es secreto.
        Verificacion__KeyVault__Habilitado = tostring(var.enable_curp_keyvault)
        Verificacion__KeyVault__VaultUri   = trimsuffix(local.infra.key_vault_uri, "/")
        Verificacion__KeyVault__KeyName    = "curp-kek"
        Verificacion__KeyVault__KekVersion = var.curp_kek_version
        # Entrega de la bandeja de salida a Mensajería (spec 037 R-11 y spec 043 R-23): dirección interna del servicio; la clave va en `secret_env`.
        Services__Messaging__BaseUrl = local.cluster_address.messaging
      })
      secret_env = merge(local.redis_secret, {
        ConnectionStrings__KnockMongo                = "mongo-identity"
        Jwt__SigningKey                              = "jwt-signing-key"
        Identity__PhoneProtection__EncryptionKeys__1 = "pii-encryption-key"
        Identity__PhoneProtection__HmacKeys__1       = "pii-hmac-key"
        # HMAC de la CURP (spec 025): llave propia, distinta de `pii-hmac-key`.
        Verificacion__CurpHmac__Keys__1 = "curp-hmac-key"
        # Llave propia de Identity, distinta de `field-encryption-key` (Market) y de `pii-encryption-key`.
        Security__FieldEncryption__Keys__1 = "identity-field-encryption-key"
        # Token de servicio de Identidad hacia Mercado (historial de «Mi casa y mi auto», spec 041): Identidad lo firma y Mercado lo valida.
        Services__Market__InternalKey = "interno-key-identity"
        # Token de servicio de Identidad hacia Mensajería (avisos y bloqueos): Identidad lo firma con `avisos-key-identity` y Mensajería lo valida con la misma.
        Services__Messaging__InternalKey = "avisos-key-identity"
        # Valida los tokens de servicio de Mercado en `POST /api/v1/interno/reportes-no-terminados` (spec 053): Mercado los firma con la misma llave.
        Security__InternalTokens__MarketKey = "interno-key-market"
        # Panel interno (spec 033): llave de firma del token del panel (la misma que lee el gateway) y llave AES-256 versionada del secreto TOTP; ninguna es la de la app.
        PanelJwt__SigningKey = "panel-jwt-signing-key"
        PanelTotp__Keys__1   = "panel-totp-key"
      })
    }
    # BUG-004 (KNOCK-241): Mensajería valida el JWT de Knock y lee la lista de sesiones canceladas en Redis; sin estos dos secretos no arranca.
    messaging = {
      external = false
      # Chat de visita (spec 026): `Chat:Habilitado` no tiene valor por omisión y sin él Mensajería no arranca (KNOCK-291).
      # `Services:Market:BaseUrl` es la dirección interna de Mercado (solo host) para consultar si una conversación está permitida.
      env = merge(local.runtime, local.forwarded, {
        Chat__Habilitado          = "true"
        Services__Market__BaseUrl = local.cluster_address.market
        # Cifrado y huella de los tokens de dispositivo (spec 037, R-03): sin versión activa y llaves, `POST /api/v1/dispositivos` responde 503.
        # `WritesEnabled` solo se enciende aquí porque deploy da a todas las réplicas la misma llave de huella (ver secret_env).
        Avisos__Devices__ActiveEncryptionVersion = "1"
        Avisos__Devices__WritesEnabled           = "true"
        # Push FCM (spec 037): apagado; el proyecto Firebase no es secreto.
        Notificaciones__PushHabilitado = "false"
        Notificaciones__Fcm__ProjectId = "knock-0"
      })
      secret_env = merge(local.redis_secret, {
        ConnectionStrings__KnockMongo = "mongo-messaging"
        Jwt__SigningKey               = "jwt-signing-key"
        # Validan los tokens de servicio de Mercado y de Identidad en `/api/v1/interno/*` (spec 037 R-11): una clave por emisor, nunca `jwt-signing-key`.
        Security__InternalTokens__MarketKey   = "avisos-key-market"
        Security__InternalTokens__IdentityKey = "avisos-key-identity"
        # Llave AES-256 (Base64, 32 bytes) que cifra el token de cada dispositivo y llave HMAC (Base64, 32 bytes o más) de su huella de búsqueda; distintas entre sí y de las demás.
        Avisos__Devices__EncryptionKeys__1 = "avisos-devices-encryption-key"
        Avisos__Devices__IndexKey          = "avisos-devices-index-key"
        # Cuenta de servicio de Firebase (JSON) para el push; solo se lee con `Notificaciones__PushHabilitado=true` (apagado por omisión).
        Notificaciones__Fcm__ServiceAccount = "fcm-service-account"
      })
    }
    market = {
      external = false
      # Cifrado de campos (specs 020 y 022; FieldCipher, sección Security:FieldEncryption): la versión vigente no es secreta y la llave sí.
      env = merge(local.runtime, local.forwarded, local.seed_demo, local.content_safety_env, {
        Security__FieldEncryption__ActiveVersion = "1"
        # Reparto por oleadas (spec 028): sin este interruptor no corre.
        Oleadas__Habilitado = tostring(var.enable_oleadas)
        # Despachador de la bandeja de salida de Mercado (spec 053/054): entrega `ReporteNoTerminadoCreado` a Identidad. Apagado por omisión en el código; deploy lo enciende.
        Bandeja__Habilitada         = "true"
        Services__Identity__BaseUrl = local.cluster_address.identity
        # Aviso `opinion_oculta` al técnico (spec 061, R-07a): dirección interna de Mensajería para entregar `OpinionModerada` a `POST /api/v1/interno/avisos`.
        Services__Messaging__BaseUrl = local.cluster_address.messaging
        # Dirección base pública de las imágenes (spec 032, R-03); el módulo de imágenes la exige al arrancar.
        Imagenes__BaseUrlPublica = local.images_base_url
        # Cuentas de Blob de las imágenes (spec 032, R-02): la pública es la de la dirección base; la privada (cuarentena y archivos) sale de la infraestructura. Entran con la identidad administrada (Azure.Identity), sin llaves.
        Imagenes__CuentaPublicaUrl = "https://${local.images_host}"
        Imagenes__CuentaPrivadaUrl = trimsuffix(local.infra.storage_account_blob_endpoint, "/")
        # Host permitido de la foto del técnico (spec 046): la misma fuente que Perfil__HostsFoto__0 (una sola lista de hosts de foto).
        Tecnicos__HostsFoto__0 = local.images_host
      })
      # Market usa Redis para la caché del catálogo (spec 010, R-01/R-02): el módulo Catalog registra el cliente al arrancar.
      # `field-encryption-key` solo la lee Market (un rol por secreto y por servicio) y es distinta de `pii-encryption-key`, que es de Identity.
      secret_env = merge(local.redis_secret, {
        ConnectionStrings__KnockMongo      = "mongo-market"
        Jwt__SigningKey                    = "jwt-signing-key"
        Security__FieldEncryption__Keys__1 = "field-encryption-key"
        # Clave del cursor de la búsqueda de expertos (spec 027, KNOCK-66): sin ella Market no arranca fuera de Development.
        Busqueda__Cursor__Key = "search-cursor-key"
        # Valida los tokens de servicio de Identidad (ruta interna de historial, spec 041); mismo secreto que lee Identidad.
        Security__InternalTokens__IdentityKey = "interno-key-identity"
        # Firma los tokens de servicio de Mercado hacia Identidad (`iss` `knock-market`); Identidad la valida con el mismo secreto.
        Security__InternalTokens__MarketKey = "interno-key-market"
        # Firma los tokens de servicio de Mercado hacia Mensajería (`iss` `knock-market`, `aud` `knock-notifications`; spec 061): Mensajería los valida con el mismo secreto (`avisos-key-market`).
        Services__Messaging__InternalKey = "avisos-key-market"
        # Llave Base64 de la huella HMAC del `sub` en los enlaces privados de imágenes (spec 032): sin ella, emitir un enlace privado da 500.
        Imagenes__LlaveBusqueda = "imagenes-llave-busqueda"
        # Llave del token del panel (esquema PanelBearer de Mercado, spec 045 R-20): el mismo secreto que ya leen el gateway e Identity; sin ella las rutas /admin de Mercado no validan el token.
        PanelJwt__SigningKey = "panel-jwt-signing-key"
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
  ready_path          = null
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
  cpu                            = var.resources[each.key].cpu
  memory                         = var.resources[each.key].memory
  enable_app_insights            = true
  app_insights_connection_string = local.infra.application_insights_connection_string
  tags                           = local.tags

  # Con la sonda de disponibilidad, un servicio que usa Redis no pasa a «listo» hasta que Redis responde: Redis primero.
  depends_on = [module.redis]
}

# Permisos de la identidad de Mercado sobre las imágenes (spec 032, §7): escribe solo en `imagenes-publicas` y `imagenes-cuarentena`
# y firma enlaces de delegación de usuario con `Storage Blob Delegator` en las dos cuentas. Sin llaves compartidas.
resource "azurerm_role_assignment" "market_public_images" {
  scope                = local.infra.public_images_container_id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.service["market"].identity_principal_id
}

resource "azurerm_role_assignment" "market_quarantine" {
  scope                = local.infra.quarantine_container_id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.service["market"].identity_principal_id
}

# Identidad escribe las exportaciones de datos (spec 039, R-22) solo en `exportaciones` y firma el enlace de 24 horas con `Storage Blob Delegator`.
# Flujo privado de fotos de solicitud (specs 058 y 062): Mercado escribe y lee solo en `cuarentena` y `archivos` (sin llaves; los enlaces de subida y de lectura son SAS de delegación de usuario).
resource "azurerm_role_assignment" "market_media" {
  for_each             = { cuarentena = local.infra.media_quarantine_container_id, archivos = local.infra.media_files_container_id }
  scope                = each.value
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.service["market"].identity_principal_id
}

# Identidad envuelve y desenvuelve con la llave `curp-kek` (spec 025 R-11), solo sobre esa llave y solo con el interruptor encendido.
# En dev el rol ya se asignó a mano sobre todo el vault: bórralo o impórtalo antes de encender esto.
resource "azurerm_role_assignment" "identity_curp_kek" {
  count                = var.enable_curp_keyvault ? 1 : 0
  scope                = "${local.infra.key_vault_id}/keys/curp-kek"
  role_definition_name = "Key Vault Crypto Service Encryption User"
  principal_id         = module.service["identity"].identity_principal_id
}

resource "azurerm_role_assignment" "identity_exports" {
  scope                = local.infra.exports_container_id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.service["identity"].identity_principal_id
}

resource "azurerm_role_assignment" "identity_exports_delegator" {
  scope                = local.infra.storage_account_id
  role_definition_name = "Storage Blob Delegator"
  principal_id         = module.service["identity"].identity_principal_id
}

# Mercado llama a Content Safety con su identidad administrada (spec 031): `Cognitive Services User` solo sobre ese recurso y solo con el interruptor encendido.
resource "azurerm_role_assignment" "market_content_safety" {
  count                = var.enable_content_safety ? 1 : 0
  scope                = local.infra.content_safety_id
  role_definition_name = "Cognitive Services User"
  principal_id         = module.service["market"].identity_principal_id
}

resource "azurerm_role_assignment" "market_delegator" {
  for_each             = { privada = local.infra.storage_account_id, publica = local.infra.public_images_account_id }
  scope                = each.value
  role_definition_name = "Storage Blob Delegator"
  principal_id         = module.service["market"].identity_principal_id
}
