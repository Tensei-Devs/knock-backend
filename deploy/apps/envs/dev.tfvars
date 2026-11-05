# Fuera de la ventana de evaluación (ADR-006, punto 1): una réplica por servicio.
replicas = {
  gateway   = { min = 1, max = 1 }
  identity  = { min = 1, max = 1 }
  messaging = { min = 1, max = 1 }
  market    = { min = 1, max = 1 }
}
