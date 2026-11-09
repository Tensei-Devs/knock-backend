# Un servicio en Container Apps (spec 005, R-02 y R-05): contenedor + identidad administrada propia + acceso
# solo a los secretos que declara. Se instancia una vez por servicio (gateway, identity, messaging, market, redis).

locals {
  secret_names = distinct(values(var.secret_env))
}

resource "azurerm_user_assigned_identity" "this" {
  name                = "id-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

# Pull de imágenes con la identidad del servicio (el registro no tiene usuario administrador).
resource "azurerm_role_assignment" "acr_pull" {
  count                = var.registry_id == null ? 0 : 1
  scope                = var.registry_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.this.principal_id
}

# Acceso por secreto, no a toda la bóveda: un servicio no puede leer los secretos de otro (amenaza de la spec 005).
resource "azurerm_role_assignment" "secret_reader" {
  for_each             = toset(local.secret_names)
  scope                = "${var.key_vault_id}/secrets/${each.value}"
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.this.principal_id
}

resource "azurerm_container_app" "this" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.environment_id
  revision_mode                = "Single"
  tags                         = var.tags
  # Perfil que Azure asigna a las apps del entorno; declararlo evita un cambio sin efecto en cada plan (KNOCK-34, deploy/README.md).
  workload_profile_name = "Consumption"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.this.id]
  }

  dynamic "registry" {
    for_each = var.registry_login_server == null ? [] : [var.registry_login_server]
    content {
      server   = registry.value
      identity = azurerm_user_assigned_identity.this.id
    }
  }

  dynamic "secret" {
    for_each = toset(local.secret_names)
    content {
      name                = secret.value
      identity            = azurerm_user_assigned_identity.this.id
      key_vault_secret_id = "${var.key_vault_uri}secrets/${secret.value}" # sin versión: toma la vigente
    }
  }

  ingress {
    external_enabled = var.ingress_external
    target_port      = var.target_port
    exposed_port     = var.ingress_transport == "tcp" ? var.target_port : null
    transport        = var.ingress_transport

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name    = var.name
      image   = var.image
      cpu     = var.cpu
      memory  = var.memory
      command = var.command
      args    = var.args

      dynamic "env" {
        for_each = var.env
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.secret_env
        content {
          name        = env.key
          secret_name = env.value
        }
      }

      dynamic "env" {
        for_each = var.enable_app_insights ? [1] : []
        content {
          name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
          value = var.app_insights_connection_string
        }
      }

      # Arranque en frío de .NET: da margen antes de que la sonda de vida pueda reiniciar el contenedor.
      dynamic "startup_probe" {
        for_each = var.health_path == null ? [] : [var.health_path]
        content {
          transport               = "HTTP"
          path                    = startup_probe.value
          port                    = var.target_port
          interval_seconds        = 5
          timeout                 = 3
          failure_count_threshold = 10
        }
      }

      dynamic "readiness_probe" {
        for_each = var.ready_path == null ? [] : [var.ready_path]
        content {
          transport               = "HTTP"
          path                    = readiness_probe.value
          port                    = var.target_port
          interval_seconds        = 10
          timeout                 = 3
          failure_count_threshold = 3
          success_count_threshold = 1
        }
      }

      dynamic "liveness_probe" {
        for_each = var.health_path == null ? [] : [var.health_path]
        content {
          transport = "HTTP"
          path      = liveness_probe.value
          port      = var.target_port
        }
      }
    }
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.secret_reader,
  ]
}
