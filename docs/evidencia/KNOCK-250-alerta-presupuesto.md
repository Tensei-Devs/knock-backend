# Evidencia — KNOCK-250 · TT-OBS-02 Alerta de consumo de presupuesto (R-05 de la spec 011)

> Spec: `specs/011-observabilidad-y-registro/spec.md` (Aprobada), R-05: «Hay alertas para picos de errores y para el consumo del presupuesto». · Fecha: 2026-11-10
> Los comandos los ejecutó el Tech Lead en su terminal y en el portal; ningún agente cambió nada en Azure. Esta nota resume lo que reportó.
> Se ocultaron el ID de suscripción y el correo (`<id-de-suscripcion>`, `<correo-del-responsable>`).

## Resumen
| Parte de la R-05 | Dónde se cubre | Estado |
|---|---|---|
| Alerta por pico de errores | PR #60 (KNOCK-206): `azurerm_monitor_metric_alert` sobre `requests/failed`, caso `_CA_011_03` | En revisión |
| Alerta por consumo del presupuesto | `budget-knock-dev` (sección 1); no requiere Terraform: el presupuesto es de nivel suscripción y queda fuera del módulo | Configurada y verificada; **falta comprobar que llega el correo** (sección 2) |

## 1. Configuración de `budget-knock-dev`
Creado en KNOCK-27 (ver `docs/evidencia/KNOCK-27-azure.md`, sección 3) sobre el grupo de recursos `rg-knock-dev`. Revisado de nuevo el 2026-11-10:

| Campo | Valor |
|---|---|
| Monto | 10 USD al mes |
| Gasto al 2026-11-10 | 3.70 USD (37 %) |
| Notificaciones habilitadas | 4: al 50, 80 y 100 % de lo gastado y al 100 % de lo previsto |
| Destino | Correo directo a `<correo-del-responsable>` (sin grupo de acciones) |
| Disparos hasta la fecha | Ninguno: el gasto no ha cruzado el 50 % |

Las alertas de presupuesto solo avisan, no frenan el gasto (ver `docs/evidencia/KNOCK-145-costo-cero.md`, punto 4).

## 2. Prueba de llegada del correo
**Pendiente.** Método: el Tech Lead crea un presupuesto temporal con un monto menor a lo ya gastado para forzar el disparo, espera el correo (Azure puede tardar varias horas en evaluarlo) y después borra el presupuesto de prueba.

| Dato | Valor |
|---|---|
| Fecha y hora de llegada | `<pendiente>` |
| Destinatario | `<correo-del-responsable>` |
| Presupuesto de prueba borrado | `<pendiente>` |

## 3. Riesgo residual
- El correo llega a una sola persona y no hay grupo de acciones: si esa persona no lo ve, nadie más se entera. Es aceptable para la evaluación y se revisa si el equipo crece.
