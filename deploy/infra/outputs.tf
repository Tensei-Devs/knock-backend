output "container_app_environment_id" {
  value = module.shared.container_app_environment_id
}

output "container_app_environment_domain" {
  value = module.shared.container_app_environment_domain
}

output "registry_id" {
  value = module.shared.registry_id
}

output "registry_login_server" {
  value = module.shared.registry_login_server
}

output "key_vault_id" {
  value = module.shared.key_vault_id
}

output "key_vault_uri" {
  value = module.shared.key_vault_uri
}

output "storage_account_name" {
  value = module.shared.storage_account_name
}

output "application_insights_connection_string" {
  value     = module.shared.application_insights_connection_string
  sensitive = true
}
