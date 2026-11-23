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

variable "enable_azure_openai" {
  description = "Crea el recurso de Azure OpenAI del asistente (spec 052). **Apagado por omisión: cuesta por token.** Enciéndelo solo con permiso de José y con el tope de `azure_openai_capacity_k_tpm` revisado."
  type        = bool
  default     = false
}

variable "enable_curp_kek" {
  description = "Declara en Terraform la llave maestra `curp-kek` de la verificación de identidad (spec 025). **Apagado por omisión.** En dev la llave ya existe: impórtala antes de encender (ver `main.tf`)."
  type        = bool
  default     = false
}

variable "azure_openai_model" {
  description = "Modelo del despliegue. Pon uno disponible en la región y suscripción (verifícalo con `az cognitiveservices model list`); no se inventa un valor por omisión que pueda no existir."
  type        = string
  default     = ""
}

variable "azure_openai_model_version" {
  type    = string
  default = ""
}

variable "azure_openai_capacity_k_tpm" {
  description = "Tope de tokens por minuto del despliegue, en miles (1 = 1 000 tokens por minuto). Es el límite de gasto por unidad de tiempo: con el valor por omisión, como máximo 1 000 tokens por minuto (≈ 1.4 millones al día si se usara sin parar). Súbelo solo a propósito."
  type        = number
  default     = 1

  validation {
    condition     = var.azure_openai_capacity_k_tpm >= 1 && var.azure_openai_capacity_k_tpm <= 10
    error_message = "El tope de tokens por minuto debe estar entre 1 y 10 (miles)."
  }
}
