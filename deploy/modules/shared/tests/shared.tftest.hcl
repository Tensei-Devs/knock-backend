# Spec 005, R-06 y CA-01: la región está limitada a las cinco que permite la suscripción y el módulo compartido
# no crea ningún servicio. Se ejecuta con `terraform test` sin tocar Azure (proveedor simulado).

mock_provider "azurerm" {
  mock_data "azurerm_resource_group" {
    defaults = {
      name     = "rg-prueba"
      location = "northcentralus"
    }
  }
  mock_data "azurerm_client_config" {
    defaults = {
      tenant_id = "00000000-0000-0000-0000-000000000000"
    }
  }
}

variables {
  environment         = "dev"
  name_suffix         = "ab12"
  resource_group_name = "rg-prueba"
}

run "region_permitida_CA_01" {
  command = plan
  variables {
    location = "westus"
  }

  assert {
    condition     = azurerm_container_registry.this.admin_enabled == false
    error_message = "El registro no debe tener usuario administrador: cada servicio usa su identidad."
  }

  assert {
    condition     = azurerm_key_vault.this.rbac_authorization_enabled
    error_message = "Key Vault debe usar RBAC, no políticas de acceso."
  }
}

run "region_fuera_de_la_politica_R_06" {
  command = plan
  variables {
    location = "eastus"
  }
  expect_failures = [var.location]
}

run "sufijo_invalido" {
  command = plan
  variables {
    location    = "westus"
    name_suffix = "NO VALE"
  }
  expect_failures = [var.name_suffix]
}
