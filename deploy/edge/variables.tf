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

variable "resource_group_name" {
  type = string
}

variable "gateway_host" {
  description = "FQDN del gateway sin https://. Vacío = se lee de la salida gateway_url del estado de apps/."
  type        = string
  default     = ""
}

variable "static_host" {
  description = "Origen del contenido estático del panel y la landing (por ejemplo el endpoint web de una cuenta de almacenamiento). Vacío = sin ruta estática."
  type        = string
  default     = ""
}

variable "public_images_host" {
  description = "Almacenamiento público de imágenes (ADR-007). Vacío = sin ruta de imágenes."
  type        = string
  default     = ""
}
