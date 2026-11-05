variable "subscription_id" {
  type = string
}

variable "use_oidc" {
  type    = bool
  default = false
}

variable "resource_group_name" {
  description = "Grupo de recursos donde ya existe el clúster."
  type        = string
}

variable "cluster_name" {
  description = "Nombre del clúster gratuito de Azure DocumentDB que ya existe."
  type        = string
}

variable "location" {
  description = "West US: el nivel gratuito solo existe en West US, France Central y Norway East (ADR-006)."
  type        = string
  default     = "westus"
}
