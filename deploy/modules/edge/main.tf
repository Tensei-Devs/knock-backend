# Borde de distribución (ADR-007, actualización de 2026-11-21): Azure Front Door Standard delante del gateway y,
# de forma opcional, del contenido estático del panel y de la landing y de las imágenes públicas.
# NO se despliega desde el CI: la persona con permisos en Azure corre `terraform apply` (ver deploy/edge/README.md).
# Las fotos de verificación y cualquier contenido privado no se enrutan por aquí (ADR-007, punto 4).

locals {
  name       = "afd-knock-${var.environment}-${var.name_suffix}"
  has_static = var.static_host != ""
  has_images = var.public_images_host != ""
}

resource "azurerm_cdn_frontdoor_profile" "this" {
  name                = local.name
  resource_group_name = var.resource_group_name
  sku_name            = var.sku_name
  tags                = var.tags
}

resource "azurerm_cdn_frontdoor_endpoint" "this" {
  name                     = "fde-knock-${var.environment}-${var.name_suffix}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id
  tags                     = var.tags
}

# ---- Gateway (API): sin caché, todo pasa al origen ----
resource "azurerm_cdn_frontdoor_origin_group" "gateway" {
  name                     = "gateway"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id
  session_affinity_enabled = false

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }

  health_probe {
    path                = "/health"
    protocol            = "Https"
    request_type        = "HEAD"
    interval_in_seconds = 100
  }
}

resource "azurerm_cdn_frontdoor_origin" "gateway" {
  name                           = "gateway"
  cdn_frontdoor_origin_group_id  = azurerm_cdn_frontdoor_origin_group.gateway.id
  enabled                        = true
  host_name                      = var.gateway_host
  origin_host_header             = var.gateway_host
  https_port                     = 443
  http_port                      = 80
  certificate_name_check_enabled = true
  priority                       = 1
  weight                         = 1000
}

resource "azurerm_cdn_frontdoor_route" "gateway" {
  name                          = "gateway"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.this.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.gateway.id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.gateway.id]
  patterns_to_match             = ["/api/*", "/health", "/health/*"]
  supported_protocols           = ["Http", "Https"]
  https_redirect_enabled        = true
  forwarding_protocol           = "HttpsOnly"
  link_to_default_domain        = true
}

# ---- Contenido estático del panel y la landing (opcional) ----
resource "azurerm_cdn_frontdoor_origin_group" "static" {
  count                    = local.has_static ? 1 : 0
  name                     = "estatico"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }

  health_probe {
    path                = "/"
    protocol            = "Https"
    request_type        = "HEAD"
    interval_in_seconds = 100
  }
}

resource "azurerm_cdn_frontdoor_origin" "static" {
  count                          = local.has_static ? 1 : 0
  name                           = "estatico"
  cdn_frontdoor_origin_group_id  = azurerm_cdn_frontdoor_origin_group.static[0].id
  enabled                        = true
  host_name                      = var.static_host
  origin_host_header             = var.static_host
  https_port                     = 443
  http_port                      = 80
  certificate_name_check_enabled = true
  priority                       = 1
  weight                         = 1000
}

resource "azurerm_cdn_frontdoor_route" "static" {
  count                         = local.has_static ? 1 : 0
  name                          = "estatico"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.this.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.static[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.static[0].id]
  patterns_to_match             = var.static_path_pattern
  supported_protocols           = ["Http", "Https"]
  https_redirect_enabled        = true
  forwarding_protocol           = "HttpsOnly"
  link_to_default_domain        = true

  cache {
    query_string_caching_behavior = "IgnoreQueryString"
    compression_enabled           = true
    content_types_to_compress     = ["text/html", "text/css", "application/javascript", "application/json", "image/svg+xml"]
  }
}

# ---- Imágenes públicas (opcional; ADR-007): nombres inmutables por hash, caché larga ----
resource "azurerm_cdn_frontdoor_origin_group" "images" {
  count                    = local.has_images ? 1 : 0
  name                     = "imagenes"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }
}

resource "azurerm_cdn_frontdoor_origin" "images" {
  count                          = local.has_images ? 1 : 0
  name                           = "imagenes"
  cdn_frontdoor_origin_group_id  = azurerm_cdn_frontdoor_origin_group.images[0].id
  enabled                        = true
  host_name                      = var.public_images_host
  origin_host_header             = var.public_images_host
  https_port                     = 443
  http_port                      = 80
  certificate_name_check_enabled = true
  priority                       = 1
  weight                         = 1000
}

resource "azurerm_cdn_frontdoor_route" "images" {
  count                         = local.has_images ? 1 : 0
  name                          = "imagenes"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.this.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.images[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.images[0].id]
  patterns_to_match             = ["/imagenes-publicas/*"]
  supported_protocols           = ["Https"]
  https_redirect_enabled        = true
  forwarding_protocol           = "HttpsOnly"
  link_to_default_domain        = true

  cache {
    query_string_caching_behavior = "IgnoreQueryString"
    compression_enabled           = false
  }
}
