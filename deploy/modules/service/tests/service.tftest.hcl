# Spec 005, R-05 y la amenaza «un servicio lee secretos de otro»: cada servicio recibe una identidad propia y
# permiso solo sobre los secretos que declara; solo el gateway puede tener ingreso externo.

mock_provider "azurerm" {}

variables {
  name                  = "knock-prueba"
  location              = "westus"
  resource_group_name   = "rg-prueba"
  environment_id        = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.App/managedEnvironments/cae"
  image                 = "acr.azurecr.io/knock-prueba:abc123"
  registry_id           = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.ContainerRegistry/registries/acr"
  registry_login_server = "acr.azurecr.io"
  key_vault_id          = "/subscriptions/0/resourceGroups/rg-prueba/providers/Microsoft.KeyVault/vaults/kv"
  key_vault_uri         = "https://kv.vault.azure.net/"
  secret_env = {
    ConnectionStrings__KnockMongo = "mongo-identity"
    Jwt__SigningKey               = "jwt-signing-key"
  }
}

run "solo_lee_sus_secretos_R_05" {
  command = plan

  assert {
    condition     = length(azurerm_role_assignment.secret_reader) == 2
    error_message = "Debe haber un permiso por secreto declarado, no uno para toda la bóveda."
  }

  assert {
    condition = alltrue([
      for k, v in azurerm_role_assignment.secret_reader : strcontains(v.scope, "/secrets/")
    ])
    error_message = "El permiso debe estar al nivel del secreto."
  }
}

run "ingreso_interno_por_defecto" {
  command = plan

  assert {
    condition     = azurerm_container_app.this.ingress[0].external_enabled == false
    error_message = "Un servicio no es público salvo que se pida (spec 001, R-02)."
  }
}

run "sin_registro_no_hay_acrpull" {
  command = plan
  variables {
    registry_id           = null
    registry_login_server = null
  }

  assert {
    condition     = length(azurerm_role_assignment.acr_pull) == 0
    error_message = "Redis usa una imagen pública y no necesita AcrPull."
  }
}

run "replicas_fuera_de_rango" {
  command = plan
  variables {
    max_replicas = 11
  }
  expect_failures = [var.max_replicas]
}

run "sondas_de_vida_arranque_y_disponibilidad" {
  command = plan

  assert {
    condition     = length(azurerm_container_app.this.template[0].container[0].readiness_probe) == 1
    error_message = "Un servicio con datos debe declarar la sonda de disponibilidad (/health/ready)."
  }

  assert {
    condition     = length(azurerm_container_app.this.template[0].container[0].startup_probe) == 1
    error_message = "Debe declarar la sonda de arranque para el arranque en frío de .NET."
  }
}

run "redis_sin_sondas_http" {
  command = plan
  variables {
    health_path = null
    ready_path  = null
  }

  assert {
    condition     = length(azurerm_container_app.this.template[0].container[0].readiness_probe) == 0 && length(azurerm_container_app.this.template[0].container[0].startup_probe) == 0 && length(azurerm_container_app.this.template[0].container[0].liveness_probe) == 0
    error_message = "Redis usa TCP: no lleva sondas HTTP."
  }
}

# Spec 034 T-01 (R-02, R-03; CA-02, CA-10): valores exactos de las sondas y gracia de 30 s; la vida nunca apunta a /health/ready.
run "sondas_exactas_y_gracia_CA_034_02" {
  command = plan

  assert {
    condition = (
      azurerm_container_app.this.template[0].container[0].startup_probe[0].interval_seconds == 5
      && azurerm_container_app.this.template[0].container[0].startup_probe[0].failure_count_threshold == 10
      && azurerm_container_app.this.template[0].container[0].readiness_probe[0].interval_seconds == 10
      && azurerm_container_app.this.template[0].container[0].readiness_probe[0].timeout == 3
      && azurerm_container_app.this.template[0].container[0].readiness_probe[0].failure_count_threshold == 3
      && azurerm_container_app.this.template[0].container[0].liveness_probe[0].interval_seconds == 10
      && azurerm_container_app.this.template[0].container[0].liveness_probe[0].failure_count_threshold == 3
    )
    error_message = "Las sondas no coinciden con la spec 034 R-02."
  }

  assert {
    condition     = azurerm_container_app.this.template[0].container[0].liveness_probe[0].path == "/health" && azurerm_container_app.this.template[0].container[0].readiness_probe[0].path == "/health/ready"
    error_message = "La sonda de vida debe apuntar a /health y la de disponibilidad a /health/ready."
  }

  assert {
    condition     = azurerm_container_app.this.template[0].termination_grace_period_seconds == 30
    error_message = "El periodo de gracia debe ser de 30 s."
  }
}
