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
