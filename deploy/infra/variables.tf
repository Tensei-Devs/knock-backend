variable "subscription_id" {
  description = "ID de la suscripción de Azure."
  type        = string
}

variable "use_oidc" {
  description = "true en el pipeline (identidad federada); false en la máquina de quien despliega."
  type        = bool
  default     = false
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "name_suffix" {
  type = string
}

variable "location" {
  description = "West US (ADR-006): la base y los servicios van en la misma región."
  type        = string
  default     = "westus"
}

variable "resource_group_name" {
  type = string
}

variable "secret_admin_object_ids" {
  type    = list(string)
  default = []
}

variable "alert_email_receiver" {
  description = "Correo que recibe las alertas del grupo de acciones (spec 011, CA-03). Vacío crea el grupo sin receptores; la dirección nunca vive en el repositorio."
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
}
