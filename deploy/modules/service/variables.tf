variable "name" {
  description = "Nombre de la Container App (máximo 32 caracteres)."
  type        = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "environment_id" {
  description = "Entorno de Container Apps (salida del módulo compartido)."
  type        = string
}

variable "image" {
  description = "Imagen completa con etiqueta."
  type        = string
}

variable "registry_id" {
  description = "ID del ACR para el rol AcrPull. null si la imagen es pública (Redis)."
  type        = string
  default     = null
}

variable "registry_login_server" {
  type    = string
  default = null
}

variable "key_vault_id" {
  type = string
}

variable "key_vault_uri" {
  description = "Termina en barra, por ejemplo https://kv-xxx.vault.azure.net/"
  type        = string
}

variable "env" {
  description = "Variables de entorno sin secretos."
  type        = map(string)
  default     = {}
}

variable "secret_env" {
  description = "Variable de entorno -> nombre del secreto en Key Vault. El valor nunca pasa por Terraform."
  type        = map(string)
  default     = {}
}

variable "ingress_external" {
  description = "Solo el gateway tiene ingreso externo (spec 001, R-02)."
  type        = bool
  default     = false
}

variable "ingress_transport" {
  type    = string
  default = "http"

  validation {
    condition     = contains(["http", "tcp"], var.ingress_transport)
    error_message = "ingress_transport debe ser http o tcp."
  }
}

variable "target_port" {
  type    = number
  default = 8080
}

variable "health_path" {
  description = "Ruta de la prueba de vida. null la desactiva (Redis usa TCP)."
  type        = string
  default     = "/health"
}

variable "min_replicas" {
  type    = number
  default = 1

  validation {
    condition     = var.min_replicas >= 0 && var.min_replicas <= 10
    error_message = "min_replicas debe estar entre 0 y 10."
  }
}

variable "max_replicas" {
  type    = number
  default = 1

  validation {
    condition     = var.max_replicas >= 1 && var.max_replicas <= 10
    error_message = "max_replicas debe estar entre 1 y 10."
  }
}

variable "cpu" {
  type    = number
  default = 0.25
}

variable "memory" {
  type    = string
  default = "0.5Gi"
}

variable "command" {
  type    = list(string)
  default = null
}

variable "args" {
  type    = list(string)
  default = null
}

variable "tags" {
  type    = map(string)
  default = {}
}

variable "enable_app_insights" {
  description = "Agrega APPLICATIONINSIGHTS_CONNECTION_STRING (spec 011). Va aparte de `env` porque el valor es sensible."
  type        = bool
  default     = false
}

variable "app_insights_connection_string" {
  type      = string
  default   = null
  sensitive = true
}
