# ADR-007 (actualización): el módulo de borde crea Front Door Standard, enruta /api/* al gateway y no crea rutas
# opcionales si no se pasan sus orígenes. Se ejecuta con `terraform test` sin tocar Azure (proveedor simulado).

mock_provider "azurerm" {}

variables {
  environment         = "dev"
  name_suffix         = "ab12"
  resource_group_name = "rg-prueba"
  gateway_host        = "knock-gateway.example.westus.azurecontainerapps.io"
}

run "solo_gateway" {
  command = plan

  assert {
    condition     = azurerm_cdn_frontdoor_profile.this.sku_name == "Standard_AzureFrontDoor"
    error_message = "Por costo el perfil debe ser Standard (Premium cuesta unos 330 USD al mes)."
  }

  assert {
    condition     = azurerm_cdn_frontdoor_origin.gateway.certificate_name_check_enabled
    error_message = "El origen debe validar el nombre del certificado."
  }

  assert {
    condition     = azurerm_cdn_frontdoor_route.gateway.forwarding_protocol == "HttpsOnly" && azurerm_cdn_frontdoor_route.gateway.https_redirect_enabled
    error_message = "La ruta del gateway solo habla HTTPS con el origen y redirige HTTP a HTTPS."
  }

  assert {
    condition     = length(azurerm_cdn_frontdoor_route.static) == 0 && length(azurerm_cdn_frontdoor_route.images) == 0
    error_message = "Sin orígenes opcionales no se crean sus rutas."
  }
}

run "con_estatico_e_imagenes" {
  command = plan
  variables {
    static_host        = "stweb.z22.web.core.windows.net"
    public_images_host = "stpubab12.blob.core.windows.net"
  }

  assert {
    condition     = length(azurerm_cdn_frontdoor_route.static) == 1 && length(azurerm_cdn_frontdoor_route.images) == 1
    error_message = "Con origen estático y de imágenes se crean sus rutas."
  }

  assert {
    condition     = azurerm_cdn_frontdoor_route.images[0].patterns_to_match == tolist(["/imagenes-publicas/*"])
    error_message = "Solo el contenedor público de imágenes pasa por la CDN; las fotos de verificación nunca."
  }
}

run "gateway_con_esquema_se_rechaza" {
  command = plan
  variables {
    gateway_host = "https://knock-gateway.example.io"
  }
  expect_failures = [var.gateway_host]
}
