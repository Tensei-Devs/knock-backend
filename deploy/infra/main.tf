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

# Azure OpenAI del asistente (spec 052): detrás de `enable_azure_openai` (apagado). Sin llaves: Market entra con su identidad administrada (`Cognitive Services OpenAI User`).
resource "azurerm_cognitive_account" "openai" {
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

  # Identidad propia de la cuenta: la usa para leer la llave de cifrado (Key Vault) y no hay llaves compartidas.
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

# Cifrado con llave propia (CKV2_AZURE_22): la llave vive en el Key Vault de Knock; la identidad de la cuenta solo puede usarla para envolver y desenvolver (rol `Key Vault Crypto Service Encryption User`).
# Quien despliega necesita `Key Vault Crypto Officer` para crear la llave (ver PARA_JOSE.md).
resource "azurerm_key_vault_key" "openai" {
  count        = var.enable_azure_openai ? 1 : 0
  name         = "oai-cmk-${var.environment}"
  key_vault_id = module.shared.key_vault_id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]
}

resource "azurerm_role_assignment" "openai_cmk" {
  count                = var.enable_azure_openai ? 1 : 0
  scope                = module.shared.key_vault_id
  role_definition_name = "Key Vault Crypto Service Encryption User"
  principal_id         = azurerm_cognitive_account.openai[0].identity[0].principal_id
}

resource "azurerm_cognitive_account_customer_managed_key" "openai" {
  count                = var.enable_azure_openai ? 1 : 0
  cognitive_account_id = azurerm_cognitive_account.openai[0].id
  key_vault_key_id     = azurerm_key_vault_key.openai[0].id

  depends_on = [azurerm_role_assignment.openai_cmk]
}
