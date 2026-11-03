# Spec 012 — Versionamiento por puntos

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir cómo se numeran las versiones de cada componente, cuántos puntos vale cada cambio y cómo se calcula la versión en cada despliegue.

## 2. Alcance
**Dentro**
- Formato `X.Y.Z` por componente (cada servicio, gateway, app, panel y landing).
- Tabla de puntos por tipo de cambio.
- Reglas de acumulación y de cambios que rompen compatibilidad.
- Etiqueta `puntos-<componente>-N` en cada PR.
- Etiqueta de Git, imagen de contenedor y registro de versiones desplegadas.

**Fuera**
- El `versionCode` de la app (frontend).

## 3. Requisitos
- **R-01:** Cada componente lleva su propia versión `X.Y.Z` y la primera versión publicada es `1.0.0`.
- **R-02:** `X` sube cuando un cambio rompe lo anterior, `Y` cuando se acumulan funciones nuevas y `Z` con cambios pequeños.
- **R-03:** Cada cambio vale puntos según su tipo: 1 por corrección, ajuste o índice; 2 por campo nuevo o cambio de regla; 3 por endpoint o pantalla nueva; 5 por colección nueva o función completa; 10 por servicio, integración externa o permiso nuevo.
- **R-04:** Cada PR declara sus puntos por componente con la etiqueta `puntos-<componente>-N`.
- **R-05:** Al desplegar se suman los puntos de los cambios incluidos y se aplican a `Z`; al llegar a 10, `Z` vuelve a cero y `Y` sube, y lo mismo entre `Y` y `X`.
- **R-06:** Un cambio que rompe compatibilidad sube `X` y deja `Y` y `Z` en cero.
- **R-07:** Cada despliegue crea una etiqueta de Git y una imagen con la misma versión; un despliegue con cero puntos no cambia la versión.
- **R-08:** Se mantiene un registro de versiones desplegadas con versión, fecha, cambios y puntos.

## 4. Criterios de aceptación
- **CA-01:** Dada la versión `1.0.0`, cuando se despliegan un cambio de 1 punto y un endpoint nuevo de 3, entonces la versión es `1.0.4`.
- **CA-02:** Dada la versión `1.0.4`, cuando se despliegan 8 puntos más, entonces la versión es `1.1.2`.
- **CA-03:** Dado un cambio que rompe compatibilidad, cuando se despliega, entonces sube `X` y `Y` y `Z` quedan en cero.
- **CA-04:** Dado un PR sin etiqueta de puntos, cuando corre la comprobación, entonces falla.
- **CA-05:** Dado un despliegue con cero puntos, cuando termina, entonces la versión no cambia.

## 5. Contrato
Fija el nombre de la etiqueta de PR `puntos-<componente>-N` y el formato de la etiqueta de Git `<componente>-vX.Y.Z` y de la imagen.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Una versión repetida sobrescribe una imagen ya desplegada | Las etiquetas de imagen son inmutables y nunca se usa `latest` |
| Puntos declarados de menos para no subir de versión | La revisión del PR comprueba la etiqueta contra el tipo de cambio |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Cálculo de la versión con los ejemplos de la descripción |
| Integración | Sí | Script de despliegue sobre un repositorio de prueba |
| Seguridad negativa | No | No aplica |
| Arquitectura | No | No aplica |
| Contrato | No | No aplica |

## 8. Decisiones
- **Versión por componente:** un cambio en la landing no debe cambiar el número del backend.
- **Puntos por tipo de cambio y no por tamaño de código:** da una regla simple y verificable.

## 9. Riesgos y dudas abiertas
- Mientras la comprobación automática no exista, la etiqueta de puntos la revisa una persona.
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
