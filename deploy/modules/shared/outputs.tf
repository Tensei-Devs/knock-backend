output "container_app_environment_id" {
  value = azurerm_container_app_environment.this.id
}

output "container_app_environment_domain" {
  value = azurerm_container_app_environment.this.default_domain
}

output "registry_id" {
  value = azurerm_container_registry.this.id
}

output "registry_login_server" {
  value = azurerm_container_registry.this.login_server
}

output "key_vault_id" {
  value = azurerm_key_vault.this.id
}

output "key_vault_uri" {
  value = azurerm_key_vault.this.vault_uri
}

output "storage_account_name" {
  value = azurerm_storage_account.files.name
}

output "application_insights_connection_string" {
  value     = azurerm_application_insights.this.connection_string
  sensitive = true
}
