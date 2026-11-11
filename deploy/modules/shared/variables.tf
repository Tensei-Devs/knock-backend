variable "environment" {
  description = "Nombre corto del entorno (dev, evaluacion)."
  type        = string
}

variable "name_suffix" {
  description = "Sufijo para los nombres únicos de Azure (Key Vault, Storage, ACR). Solo letras minúsculas y números."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{2,8}$", var.name_suffix))
    error_message = "name_suffix debe tener de 2 a 8 caracteres entre a-z y 0-9."
  }
}

variable "location" {
  description = "Región de Azure. La suscripción de estudiante solo permite cinco; el backend va en West US (ADR-006)."
  type        = string

  validation {
    condition     = contains(["westus", "chilecentral", "norwayeast", "francecentral", "northcentralus"], var.location)
    error_message = "La política de la suscripción solo permite westus, chilecentral, norwayeast, francecentral y northcentralus."
  }
}

variable "resource_group_name" {
  description = "Grupo de recursos existente (puede estar en otra región que los recursos)."
  type        = string
}

variable "secret_admin_object_ids" {
  description = "Object IDs de las personas que pueden escribir secretos en Key Vault."
  type        = list(string)
  default     = []
}

variable "log_daily_quota_gb" {
  description = "Tope diario de ingesta de logs para no gastar el crédito."
  type        = number
  default     = 0.5
}

variable "alert_email_receiver" {
  description = "Correo que recibe las alertas del grupo de acciones (spec 011, CA-03). Sin valor por defecto real: vacío crea el grupo sin receptores. La dirección nunca vive en el repositorio."
  type        = string
  default     = ""

  validation {
    condition     = can(regex("^$|^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.alert_email_receiver))
    error_message = "alert_email_receiver debe estar vacío o ser un correo válido (texto@dominio.tld)."
  }
}

variable "alert_failed_requests_threshold" {
  description = "Peticiones fallidas (requests/failed) en la ventana de 5 minutos que disparan la alerta (spec 011, CA-03)."
  type        = number
  default     = 10

  validation {
    condition     = var.alert_failed_requests_threshold >= 1
    error_message = "alert_failed_requests_threshold debe ser al menos 1."
  }
}

variable "vnet_cidr" {
  type    = string
  default = "10.20.0.0/16"
}

variable "container_apps_subnet_cidr" {
  type    = string
  default = "10.20.0.0/23"
}

variable "tags" {
  type    = map(string)
  default = {}
}
