# El clúster gratuito de DocumentDB es único por suscripción (spec 005, R-07): este directorio solo lo IMPORTA y
# tiene su propio estado, así que ni «eliminar lo desplegado» ni «destruir» (que operan sobre `apps` e `infra`)
# lo tocan (CA-03). Además `prevent_destroy` impide borrarlo por error aquí mismo.
#
#   terraform init -backend-config=backend.hcl
#   terraform plan -var-file=... # debe decir "1 to import" la primera vez y "No changes" después (CA-02)

data "azurerm_client_config" "current" {}

import {
  to = azurerm_mongo_cluster.free
  id = "/subscriptions/${var.subscription_id}/resourceGroups/${var.resource_group_name}/providers/Microsoft.DocumentDB/mongoClusters/${var.cluster_name}"
}

resource "azurerm_mongo_cluster" "free" {
  name                   = var.cluster_name
  resource_group_name    = var.resource_group_name
  location               = var.location
  compute_tier           = "Free"
  high_availability_mode = "Disabled"
  shard_count            = 1
  storage_size_in_gb     = 32

  lifecycle {
    prevent_destroy = true
    # Las credenciales de administrador no se pueden leer de vuelta de Azure y no pasan por Terraform.
    ignore_changes = [administrator_username, administrator_password, version, storage_size_in_gb]
  }
}
