terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # Estado remoto propio (como infra, database y apps): destruir el borde no toca lo demás.
  # Los valores van en `backend.hcl` (no son secretos): terraform init -backend-config=backend.hcl
  backend "azurerm" {}
}

provider "azurerm" {
  features {}
  subscription_id     = var.subscription_id
  use_oidc            = var.use_oidc
  storage_use_azuread = true
}
