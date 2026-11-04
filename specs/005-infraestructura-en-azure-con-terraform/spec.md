# Spec 005 — Infraestructura en Azure con Terraform

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir en qué nube corre el backend, qué servicios usa y cómo se crea y se borra toda la infraestructura con comandos.

## 2. Alcance
**Dentro**
- Elección de la nube y los servicios de Azure que se usan.
- Módulos de Terraform (uno por servicio y uno compartido).
- Estado remoto, identidades y secretos.
- Los cuatro pasos manuales: crear, desplegar, eliminar lo desplegado y destruir.
- Región y límites de la suscripción de estudiante.

**Fuera**
- Pipeline de despliegue completo (spec 007).
- Estimación detallada de costos.

## 3. Requisitos
- **R-01:** Toda la infraestructura se describe con Terraform y se crea, cambia y borra con comandos.
- **R-02:** Hay un módulo por servicio (contenedor, identidad y secretos) y un módulo compartido (red, entorno de Container Apps, Key Vault, Blob Storage y registros).
- **R-03:** El estado de Terraform vive en un almacenamiento compartido de Azure.
- **R-04:** El pipeline entra a Azure con identidad federada y el rol mínimo sobre el grupo de recursos; no guarda contraseñas.
- **R-05:** Cada servicio lee sus secretos de Key Vault con una identidad administrada propia.
- **R-06:** Los recursos se crean solo en las cinco regiones que permite la suscripción; la base y los servicios van en la misma región, que es West US (ADR-006); el grupo de recursos puede estar en otra.
- **R-07:** El clúster gratuito de DocumentDB, único por suscripción, se importa y el paso de destruir lo excluye.

## 4. Criterios de aceptación
- **CA-01:** Dado un entorno vacío, cuando se ejecuta el paso de crear, entonces quedan la red, el entorno de contenedores, Key Vault, Blob Storage y los registros, sin servicios corriendo.
- **CA-02:** Dado `terraform plan` sin cambios, cuando se ejecuta, entonces no propone modificaciones.
- **CA-03:** Dado el paso de destruir, cuando termina, entonces el clúster gratuito de DocumentDB sigue existiendo.
- **CA-04:** Dado el repositorio, cuando se busca una contraseña o llave, entonces no se encuentra.

## 5. Contrato
No expone endpoints. Interfaces que fija: variables de Terraform (réplicas, tamaño, región, entorno), los cuatro procesos manuales y los nombres de los secretos en Key Vault.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Credenciales del pipeline filtradas | Identidad federada (OIDC): el pipeline no guarda contraseñas y su rol es mínimo sobre el grupo de recursos |
| Un servicio lee secretos de otro | Una identidad administrada por servicio con acceso solo a sus secretos |
| Se borra por accidente el clúster gratuito de base de datos | El clúster se importa y el paso de destruir lo excluye; los pasos son manuales |
| Sobrecosto por recursos olvidados | Variables de réplicas, alertas de presupuesto y revisión semanal del consumo |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validación de variables (`terraform validate`) |
| Integración | Sí | `terraform plan` contra un entorno de prueba |
| Seguridad negativa | Sí | Revisión de la infraestructura con un analizador de Terraform |
| Humo / extremo a extremo | Sí | Crear, desplegar y destruir de punta a punta |
| Arquitectura | No | No aplica a infraestructura |
| Contrato | No | No hay contrato |

## 8. Decisiones
- **Azure:** el backend es .NET; la suscripción de estudiante da crédito sin tarjeta; Container Apps incluye uso mensual gratuito; y en la misma nube están la IA, los secretos, el almacenamiento y la monitorización.
- **Terraform:** es repetible, se revisa como código y permite levantar y tumbar todo en minutos, como se establece en los requerimientos.
- **Pasos manuales:** nada se crea ni se borra por accidente.
- **Una sola región para el backend:** replicarlo multiplicaría el costo; la CDN reparte las imágenes públicas.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
