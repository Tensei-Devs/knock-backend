# Paso 1 «crear» (spec 005, CA-01): deja la red, el entorno de contenedores, Key Vault, Blob Storage y los
# registros, sin ningún servicio corriendo.
module "shared" {
  source = "../modules/shared"

  environment                     = var.environment
  name_suffix                     = var.name_suffix
  location                        = var.location
  resource_group_name             = var.resource_group_name
  secret_admin_object_ids         = var.secret_admin_object_ids
  alert_email_receiver            = var.alert_email_receiver
  alert_failed_requests_threshold = var.alert_failed_requests_threshold
}

# Azure AI Content Safety de la moderación (spec 031): SKU F0 (gratis, 5 000 textos y 5 000 imágenes al mes), detrás de `enable_content_safety` (apagado). Sin llaves: Market entra con su identidad
# administrada (`Cognitive Services User`) y el cliente `ContentSafetyClient` ya existe. Cifrado en reposo administrado por Microsoft (EXC-034).
resource "azurerm_cognitive_account" "content_safety" {
  #checkov:skip=CKV2_AZURE_22: EXC-034 sin llave propia: exige protección contra purga del Key Vault, que es irreversible; se usa el cifrado administrado por Microsoft
  count                         = var.enable_content_safety ? 1 : 0
  name                          = "cs-knock-${var.environment}-${var.name_suffix}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  kind                          = "ContentSafety"
  sku_name                      = "F0"
  custom_subdomain_name         = "cs-knock-${var.environment}-${var.name_suffix}"
  local_auth_enabled            = false
  public_network_access_enabled = true
  tags                          = { proyecto = "knock", entorno = var.environment }

  identity {
    type = "SystemAssigned"
  }
}

# Azure OpenAI del asistente (spec 052): detrás de `enable_azure_openai` (apagado). Sin llaves: Market entra con su identidad administrada (`Cognitive Services OpenAI User`).
resource "azurerm_cognitive_account" "openai" {
  #checkov:skip=CKV2_AZURE_22: EXC-034 sin llave propia: exige protección contra purga del Key Vault, que es irreversible; se usa el cifrado administrado por Microsoft
  count                         = var.enable_azure_openai ? 1 : 0
  name                          = "oai-knock-${var.environment}-${var.name_suffix}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  kind                          = "OpenAI"
  sku_name                      = "S0"
  custom_subdomain_name         = "oai-knock-${var.environment}-${var.name_suffix}"
  local_auth_enabled            = false
  public_network_access_enabled = true
  tags                          = { proyecto = "knock", entorno = var.environment }

  # Identidad propia de la cuenta; no hay llaves compartidas.
  identity {
    type = "SystemAssigned"
  }

  lifecycle {
    precondition {
      condition     = var.azure_openai_model != "" && var.azure_openai_model_version != ""
      error_message = "Con enable_azure_openai = true define azure_openai_model y azure_openai_model_version (modelos disponibles: az cognitiveservices model list)."
    }
  }
}

resource "azurerm_cognitive_deployment" "assistant" {
  count                = var.enable_azure_openai ? 1 : 0
  name                 = "asistente"
  cognitive_account_id = azurerm_cognitive_account.openai[0].id

  model {
    format  = "OpenAI"
    name    = var.azure_openai_model
    version = var.azure_openai_model_version
  }

  sku {
    name     = "Standard"
    capacity = var.azure_openai_capacity_k_tpm
  }
}

# Llave maestra de la verificación de identidad (spec 025, R-11): envuelve la llave de datos de cada verificación; no sale nunca del vault.
# Detrás de `enable_curp_kek` (apagado). La llave `curp-kek` de dev ya existe, creada a mano: antes de encender esto en ese entorno impórtala
# (`terraform import 'azurerm_key_vault_key.curp_kek[0]' <id de la llave sin versión>`) para no crear una versión nueva ni perder la KekVersion de las verificaciones guardadas.
resource "azurerm_key_vault_key" "curp_kek" {
  count        = var.enable_curp_kek ? 1 : 0
  name         = "curp-kek"
  key_vault_id = module.shared.key_vault_id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]
}
