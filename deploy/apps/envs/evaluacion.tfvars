# Ventana de evaluación, 25-nov al 5-dic de 2026 (ADR-006, punto 2): mínimo y máximo 2, sin reglas de escalado.
replicas = {
  gateway   = { min = 2, max = 2 }
  identity  = { min = 2, max = 2 }
  messaging = { min = 2, max = 2 }
  market    = { min = 2, max = 2 }
}
