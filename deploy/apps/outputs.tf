output "gateway_url" {
  description = "Única dirección pública del backend."
  value       = "https://${module.service["gateway"].fqdn}"
}
