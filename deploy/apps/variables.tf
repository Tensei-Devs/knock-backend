variable "subscription_id" {
  type = string
}

variable "use_oidc" {
  type    = bool
  default = false
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "location" {
  type    = string
  default = "westus"
}

variable "resource_group_name" {
  type = string
}

variable "image_tag" {
  description = "Etiqueta de las imágenes en el ACR (la publica el pipeline, spec 007). Usa el SHA del commit, no `latest`."
  type        = string
}

variable "panel_origin" {
  description = "Origen exacto del panel interno para CORS (spec 033, R-27): `https://host[:puerto]` sin ruta ni barra final. Vacío = ningún origen permitido. No es un secreto."
  type        = string
  default     = ""
}

variable "replicas" {
  description = <<-EOT
    Réplicas por servicio (ADR-006, spec 006 R-01 y R-06). Por defecto una réplica por servicio, sin autoescalado, para cuidar
    el costo (R-06). Para la ventana de evaluación (25-nov al 5-dic de 2026) se suben a min = max = 2 con el tfvars de ese
    entorno o con `-var 'replicas={gateway={min=2,max=2},identity={min=2,max=2},messaging={min=2,max=2},market={min=2,max=2}}'`.
    Redis siempre 1 (se fija en main.tf).
  EOT
  type = map(object({
    min = number
    max = number
  }))
  default = {
    gateway   = { min = 1, max = 1 }
    identity  = { min = 1, max = 1 }
    messaging = { min = 1, max = 1 }
    market    = { min = 1, max = 1 }
  }

  validation {
    condition     = alltrue([for k, v in var.replicas : v.min >= 0 && v.max >= v.min && v.max <= 10])
    error_message = "Cada servicio necesita 0 <= min <= max <= 10."
  }
}

variable "redis_image" {
  description = "Imagen de Redis (spec 003, R-07): una réplica, sin persistencia. 7.2 es la última con licencia BSD-3; no subir a 7.4 o posterior (RSAL/SSPL)."
  type        = string
  default     = "docker.io/library/redis:7.2-alpine@sha256:2c933526722b4126a61d5c170f766dc159adf97682f0b1cbbc27b1666ba96d84"
}

variable "container_apps_subnet_cidr" {
  description = <<-EOT
    Subred del entorno de Container Apps (la misma que `container_apps_subnet_cidr` del módulo compartido). Los servicios
    la declaran como red de confianza para leer `X-Forwarded-For`: sin ella, todos los clientes llegarían con la IP del
    ingreso y compartirían un solo cubo del límite de peticiones por IP. Nunca pongas 0.0.0.0/0: permitiría suplantar la IP.
  EOT
  type        = string
  default     = "10.20.0.0/23"

  validation {
    condition     = can(cidrnetmask(var.container_apps_subnet_cidr)) && can(regex("^(10\\.|172\\.(1[6-9]|2[0-9]|3[01])\\.|192\\.168\\.)", var.container_apps_subnet_cidr)) && try(tonumber(split("/", var.container_apps_subnet_cidr)[1]) >= 16, false)
    error_message = "container_apps_subnet_cidr debe ser una red IPv4 privada (10/8, 172.16/12 o 192.168/16) con prefijo /16 o más estrecho (spec 035, R-10)."
  }
}

variable "aspnetcore_environment" {
  description = <<-EOT
    Entorno de ASP.NET de los cuatro servicios. Los Dockerfile fijan `Production` y ese es el valor por omisión (evaluación y producción).
    Solo el tfvars de dev lo pone en `Development` (decisión de José, camino A de moderación: con `Development` el chat puede operar sin moderación
    y las llaves que faltan se generan al azar). Aun así, `Errores__ExponerDetalle=false` se manda siempre para que ninguna respuesta lleve mensajes ni trazas de excepciones.
  EOT
  type        = string
  default     = "Production"

  validation {
    condition     = contains(["Production", "Development"], var.aspnetcore_environment)
    error_message = "aspnetcore_environment solo puede ser Production o Development."
  }
}

variable "resources" {
  description = <<-EOT
    CPU (vCPU) y memoria por servicio. El crédito de Azure es limitado y el gasto es sobre todo Container Apps, así que el valor por omisión es el mínimo
    razonable: 0.25 vCPU y 0.5 GiB por servicio. Súbelos solo si un servicio se queda sin memoria. Combinaciones válidas de Container Apps: 0.25 vCPU con 0.5Gi, 0.5 con 1Gi, 0.75 con 1.5Gi, 1 con 2Gi.
  EOT
  type = map(object({
    cpu    = number
    memory = string
  }))
  default = {
    gateway   = { cpu = 0.25, memory = "0.5Gi" }
    identity  = { cpu = 0.25, memory = "0.5Gi" }
    messaging = { cpu = 0.25, memory = "0.5Gi" }
    market    = { cpu = 0.25, memory = "0.5Gi" }
  }
}

variable "seed_demo" {
  description = "Enciende la carga de datos de demostración (`Seed:Demo`) en Identity y Market. Apagado por omisión: solo se enciende si el humo de dev lo necesita (ver deploy/README.md)."
  type        = bool
  default     = false
}

variable "enable_oleadas" {
  description = "Enciende el reparto por oleadas de solicitudes (`Oleadas:Habilitado`, spec 028) en Market. Sin esto no corre."
  type        = bool
  default     = true
}
