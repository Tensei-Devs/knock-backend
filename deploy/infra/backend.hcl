# Valores no secretos del estado remoto. Cuenta y grupo ya existentes en la suscripción (rg-knock-tfstate, stknocktfstate).
resource_group_name  = "rg-knock-tfstate"
storage_account_name = "stknocktfstate"
container_name       = "tfstate"
key                  = "infra.tfstate"
use_azuread_auth     = true
