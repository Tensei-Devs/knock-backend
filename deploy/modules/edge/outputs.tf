output "endpoint_hostname" {
  description = "Nombre DNS público del borde (https://<valor>)."
  value       = azurerm_cdn_frontdoor_endpoint.this.host_name
}

output "frontdoor_id" {
  description = "Identificador de Front Door (cabecera X-Azure-FDID). Sirve para que el gateway acepte solo tráfico que pasó por el borde; esa regla NO está implementada en esta rama."
  value       = azurerm_cdn_frontdoor_profile.this.resource_guid
}

output "profile_name" {
  value = azurerm_cdn_frontdoor_profile.this.name
}
