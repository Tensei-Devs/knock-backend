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
