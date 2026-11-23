# Entorno de desarrollo (crédito de Azure limitado: el gasto es sobre todo Container Apps).
# Escala a cero cuando no hay tráfico: mínimo 0 y máximo 1 por servicio (el primer pedido tarda unos segundos en arrancar). Redis siempre 1 (se fija en main.tf).
replicas = {
  gateway   = { min = 0, max = 1 }
  identity  = { min = 0, max = 1 }
  messaging = { min = 0, max = 1 }
  market    = { min = 0, max = 1 }
}

# CPU y memoria mínimas por servicio (0.25 vCPU y 0.5 GiB); súbelas solo si un servicio se queda sin memoria.
resources = {
  gateway   = { cpu = 0.25, memory = "0.5Gi" }
  identity  = { cpu = 0.25, memory = "0.5Gi" }
  messaging = { cpu = 0.25, memory = "0.5Gi" }
  market    = { cpu = 0.25, memory = "0.5Gi" }
}

# Camino A de moderación (decisión de José): solo el entorno de dev corre como Development. Evaluación y producción siguen en Production.
# Las llaves explícitas (Jwt, búsqueda, panel) vienen de Key Vault y ganan sobre las generadas al azar; ver deploy/README.md, «Entorno de dev».
aspnetcore_environment = "Development"

# Datos de demostración apagados: solo se encienden si el humo de dev los necesita.
seed_demo = false
