# Borde de distribución (ADR-007, actualización de 2026-11-21). Estado propio: se puede destruir sin tocar infra, database ni apps.
# REQUIERE AUTORIZACIÓN: lo aplica una persona con permisos en Azure; ningún workflow lo despliega.

data "terraform_remote_state" "apps" {
  count   = var.gateway_host == "" ? 1 : 0
  backend = "azurerm"
  config = {
    resource_group_name  = "rg-knock-tfstate"
    storage_account_name = "stknocktfstate"
    container_name       = "tfstate"
    key                  = "apps.tfstate"
    use_azuread_auth     = true
    subscription_id      = var.subscription_id
    use_oidc             = var.use_oidc
  }
}

locals {
  tags = { proyecto = "knock", entorno = var.environment }
  # `gateway_url` de apps/ es «https://<fqdn>»: aquí solo hace falta el nombre DNS.
  gateway_host = var.gateway_host != "" ? var.gateway_host : replace(data.terraform_remote_state.apps[0].outputs.gateway_url, "https://", "")
}

module "edge" {
  source = "../modules/edge"

  environment         = var.environment
  name_suffix         = var.name_suffix
  resource_group_name = var.resource_group_name
  gateway_host        = local.gateway_host
  static_host         = var.static_host
  public_images_host  = var.public_images_host
  tags                = local.tags
}
