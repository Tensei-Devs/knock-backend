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
