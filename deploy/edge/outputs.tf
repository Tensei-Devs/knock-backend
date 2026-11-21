output "edge_url" {
  description = "Dirección pública del borde."
  value       = "https://${module.edge.endpoint_hostname}"
}

output "frontdoor_id" {
  description = "Valor de X-Azure-FDID que el gateway debería exigir (regla aún no implementada)."
  value       = module.edge.frontdoor_id
}
