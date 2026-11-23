output "name" {
  value = azurerm_container_app.this.name
}

output "fqdn" {
  value = azurerm_container_app.this.ingress[0].fqdn
}

output "identity_principal_id" {
  value = azurerm_user_assigned_identity.this.principal_id
}

output "min_replicas" {
  value = azurerm_container_app.this.template[0].min_replicas
}

output "max_replicas" {
  value = azurerm_container_app.this.template[0].max_replicas
}
