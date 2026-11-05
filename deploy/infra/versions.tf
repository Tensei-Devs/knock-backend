terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # Estado remoto en Azure Storage con bloqueo (spec 005, R-03; ADR-003, regla 1).
  # Los valores van en `backend.hcl` (no son secretos): terraform init -backend-config=backend.hcl
  backend "azurerm" {}
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
  # Entra con la sesión de `az login` o, en el pipeline, con identidad federada (ARM_USE_OIDC=true). Sin contraseñas.
  use_oidc            = var.use_oidc
  storage_use_azuread = true
}
