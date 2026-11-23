variable "environment" {
  description = "Nombre corto del entorno (dev, evaluacion)."
  type        = string
}

variable "name_suffix" {
  description = "Sufijo para los nombres únicos de Azure. Solo letras minúsculas y números."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{2,8}$", var.name_suffix))
    error_message = "name_suffix debe tener de 2 a 8 caracteres entre a-z y 0-9."
  }
}

variable "resource_group_name" {
  description = "Grupo de recursos existente (Terraform no lo crea)."
  type        = string
}

variable "gateway_host" {
  description = "Nombre DNS (sin https:// ni ruta) del gateway, es decir, el FQDN de su Container App. Es el origen de /api/*."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$", var.gateway_host))
    error_message = "gateway_host debe ser un nombre DNS en minúsculas, sin esquema ni ruta."
  }
}

variable "static_host" {
  description = "Nombre DNS del origen del contenido estático del panel y de la landing (por ejemplo el endpoint web de una cuenta de almacenamiento). Vacío = no se crea la ruta estática."
  type        = string
  default     = ""
}

variable "static_path_pattern" {
  description = "Patrones de ruta que atiende el origen estático."
  type        = list(string)
  default     = ["/*"]
}

variable "public_images_host" {
  description = "Nombre DNS del almacenamiento público de imágenes (ADR-007). Vacío = no se crea la ruta de imágenes. Las fotos de verificación NUNCA van por la CDN."
  type        = string
  default     = ""
}

variable "sku_name" {
  description = "Solo Standard_AzureFrontDoor (unos 35 USD al mes de base). Premium (unos 330 USD) está prohibido por ADR-007 (enmienda 2026-11-24) y spec 034 R-11."
  type        = string
  default     = "Standard_AzureFrontDoor"

  validation {
    condition     = var.sku_name == "Standard_AzureFrontDoor"
    error_message = "sku_name debe ser Standard_AzureFrontDoor: el SKU Premium está prohibido por costo (ADR-007, spec 034 CA-10)."
  }
}

variable "tags" {
  description = "Etiquetas de los recursos."
  type        = map(string)
  default     = {}
}
