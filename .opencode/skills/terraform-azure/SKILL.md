---
name: terraform-azure
description: Cómo se describe la infraestructura en Azure con Terraform: módulos, estado remoto, identidades y secretos; úsala al especificar infraestructura.
---

# terraform-azure

- Un módulo por servicio y uno compartido (red, entorno de contenedores, Key Vault, Blob y registros).
- El estado vive en un almacenamiento compartido de Azure.
- El pipeline entra a Azure con identidad federada y rol mínimo; los servicios leen sus secretos con identidad administrada propia.
- Crear, desplegar, eliminar lo desplegado y destruir son pasos manuales separados.
