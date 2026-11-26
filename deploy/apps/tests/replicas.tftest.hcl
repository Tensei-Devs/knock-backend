# Spec 034, R-01 y CA-01: perfiles de replicas de `deploy/apps/envs/*.tfvars`.
# Se corre una vez por perfil pasando su tfvars (el estado remoto de infraestructura se sustituye por valores de prueba):
#   terraform -chdir=deploy/apps init -backend=false
#   terraform -chdir=deploy/apps test -var-file=envs/evaluacion.tfvars -var resource_group_name=rg-prueba -var subscription_id=00000000-0000-0000-0000-000000000000 -var image_tag=prueba
# El perfil se reconoce por `var.aspnetcore_environment` (Production = evaluacion; Development = dev), asi que un archivo sirve a los dos.
# Discrepancia anotada: la spec pide 1 replica en dev, pero por decision de #277 dev escala a cero (min 0, max 1); aqui dev se prueba con max = 1.

mock_provider "azurerm" {}

override_data {
  target = data.terraform_remote_state.infra
  values = {
    outputs = {
      application_insights_connection_string = "InstrumentationKey=00000000-0000-0000-0000-000000000000"
      container_app_environment_id           = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.App/managedEnvironments/cae"
      content_safety_endpoint                = "https://cs.example.invalid/"
      content_safety_id                      = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.CognitiveServices/accounts/cs"
      exports_container_id                   = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/st/blobServices/default/containers/exportaciones"
      key_vault_id                           = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.KeyVault/vaults/kv"
      key_vault_uri                          = "https://kv.vault.azure.net/"
      media_files_container_id               = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/st/blobServices/default/containers/archivos"
      media_quarantine_container_id          = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/st/blobServices/default/containers/cuarentena"
      public_images_account_id               = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/stpub"
      public_images_base_url                 = "https://stpub.blob.core.windows.net/imagenes-publicas"
      public_images_container_id             = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/stpub/blobServices/default/containers/imagenes-publicas"
      quarantine_container_id                = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/stpub/blobServices/default/containers/imagenes-cuarentena"
      registry_id                            = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.ContainerRegistry/registries/acr"
      registry_login_server                  = "acr.azurecr.io"
      storage_account_blob_endpoint          = "https://st.blob.core.windows.net/"
      storage_account_id                     = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.Storage/storageAccounts/st"
    }
  }
}

run "evaluacion_dos_replicas_fijas_CA_034_01" {
  command = plan

  assert {
    condition     = var.aspnetcore_environment != "Production" || alltrue([for s in ["gateway", "identity", "messaging", "market"] : module.service[s].min_replicas == 2 && module.service[s].max_replicas == 2])
    error_message = "Evaluacion debe fijar min = max = 2 en gateway, identity, messaging y market (spec 034 R-01)."
  }
}

run "dev_nunca_pasa_de_una_replica_CA_034_01" {
  command = plan

  assert {
    condition     = var.aspnetcore_environment != "Development" || alltrue([for s in ["gateway", "identity", "messaging", "market"] : module.service[s].min_replicas <= 1 && module.service[s].max_replicas == 1])
    error_message = "Dev nunca pasa de 1 replica (min 0 o 1, max 1)."
  }
}

run "redis_una_replica_CA_034_01" {
  command = plan

  assert {
    condition     = module.redis.min_replicas == 1 && module.redis.max_replicas == 1
    error_message = "Redis siempre corre con una replica (spec 034 R-01)."
  }
}
