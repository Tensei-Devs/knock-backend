# Spec 035, R-11 y CA-14: la subred del entorno de Container Apps (la red de confianza de X-Forwarded-For) debe ser privada y de al menos /16.
# Se ejecuta con `terraform test` sin tocar Azure (proveedor simulado).

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
  location            = "westus"
}

run "subred_privada_valida_CA_14" {
  command = plan
  variables {
    container_apps_subnet_cidr = "10.20.0.0/23"
  }
}

run "subred_publica_rechazada_CA_14" {
  command = plan
  variables {
    container_apps_subnet_cidr = "203.0.113.0/24"
  }
  expect_failures = [var.container_apps_subnet_cidr]
}

run "subred_total_rechazada_CA_14" {
  command = plan
  variables {
    container_apps_subnet_cidr = "0.0.0.0/0"
  }
  expect_failures = [var.container_apps_subnet_cidr]
}

run "subred_demasiado_amplia_rechazada_CA_14" {
  command = plan
  variables {
    container_apps_subnet_cidr = "10.0.0.0/8"
  }
  expect_failures = [var.container_apps_subnet_cidr]
}
