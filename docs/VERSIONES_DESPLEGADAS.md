# Versiones desplegadas

> Registro de cada despliegue (regla 4 de [`VERSIONAMIENTO.md`](VERSIONAMIENTO.md) y R-08 de la spec 012). Una fila por despliegue y componente, con versión, fecha, cambios y puntos. Se llena a mano hasta que el pipeline de despliegue lo haga automático (T-03 de la spec 012, KNOCK-202).
>
> - **Versión:** `X.Y.Z` calculada con `python scripts/ci/version.py bump --current <anterior> --points <N>`; igual a la etiqueta de Git `<componente>-vX.Y.Z` y a la etiqueta de la imagen.
> - **Cambios:** PR incluidos (`#N`). **Puntos:** suma de las etiquetas `puntos-<componente>-N` de esos PR; con 0 puntos no se despliega una versión nueva.

| Fecha | Componente | Versión | Puntos | Cambios | Entorno | Responsable |
|---|---|---|---|---|---|---|
| 2026-11-26 | gateway | Sin asignar (imagen `cce78652`) | Sin calcular | #349 (`sprint-05` hasta #402) | dev | Manual; autorizado por José Julián Hernández Hernández |
| 2026-11-26 | identity | Sin asignar (imagen `cce78652`) | Sin calcular | #349 (`sprint-05` hasta #402) | dev | Manual; autorizado por José Julián Hernández Hernández |
| 2026-11-26 | messaging | Sin asignar (imagen `cce78652`) | Sin calcular | #349 (`sprint-05` hasta #402) | dev | Manual; autorizado por José Julián Hernández Hernández |
| 2026-11-26 | market | Sin asignar (imagen `cce78652`) | Sin calcular | #349 (`sprint-05` hasta #402) | dev | Manual; autorizado por José Julián Hernández Hernández |
| 2026-11-23 | gateway | Sin asignar (imagen `9872a0ae`) | Sin calcular | `sprint-05` hasta #339 | dev | Manual (registro del equipo) |
| 2026-11-23 | identity | Sin asignar (imagen `9872a0ae`) | Sin calcular | `sprint-05` hasta #339 | dev | Manual (registro del equipo) |
| 2026-11-23 | messaging | Sin asignar (imagen `9872a0ae`) | Sin calcular | `sprint-05` hasta #339 | dev | Manual (registro del equipo) |
| 2026-11-23 | market | Sin asignar (imagen `9872a0ae`) | Sin calcular | `sprint-05` hasta #339 | dev | Manual (registro del equipo) |
| 2026-11-23 | gateway | Sin asignar (imagen `8ab22b4b`) | Sin calcular | `sprint-05` hasta #283 | dev | Manual (registro del equipo) |
| 2026-11-23 | identity | Sin asignar (imagen `8ab22b4b`) | Sin calcular | `sprint-05` hasta #283 | dev | Manual (registro del equipo) |
| 2026-11-23 | messaging | Sin asignar (imagen `8ab22b4b`) | Sin calcular | `sprint-05` hasta #283 | dev | Manual (registro del equipo) |
| 2026-11-23 | market | Sin asignar (imagen `8ab22b4b`) | Sin calcular | `sprint-05` hasta #283 | dev | Manual (registro del equipo) |
| 2026-11-21 | gateway | Sin asignar (imagen `5e7b65c4`) | Sin calcular | #233 (`release` a `main`) | dev | Automático (`ci-deploy` 44951355609); actor JulianH2 |
| 2026-11-21 | identity | Sin asignar (imagen `5e7b65c4`) | Sin calcular | #233 (`release` a `main`) | dev | Automático (`ci-deploy` 44951355609); actor JulianH2 |
| 2026-11-21 | messaging | Sin asignar (imagen `5e7b65c4`) | Sin calcular | #233 (`release` a `main`) | dev | Automático (`ci-deploy` 44951355609); actor JulianH2 |
| 2026-11-21 | market | Sin asignar (imagen `5e7b65c4`) | Sin calcular | #233 (`release` a `main`) | dev | Automático (`ci-deploy` 44951355609); actor JulianH2 |
| 2026-11-17 | gateway | Sin asignar (imagen `0ba9f234`) | Sin calcular | #154 (`release` a `main`) | dev | Automático (`ci-deploy` 44025337346); actor JulianH2 |
| 2026-11-17 | identity | Sin asignar (imagen `0ba9f234`) | Sin calcular | #154 (`release` a `main`) | dev | Automático (`ci-deploy` 44025337346); actor JulianH2 |
| 2026-11-17 | messaging | Sin asignar (imagen `0ba9f234`) | Sin calcular | #154 (`release` a `main`) | dev | Automático (`ci-deploy` 44025337346); actor JulianH2 |
| 2026-11-17 | market | Sin asignar (imagen `0ba9f234`) | Sin calcular | #154 (`release` a `main`) | dev | Automático (`ci-deploy` 44025337346); actor JulianH2 |
| 2026-11-13 | gateway | Sin asignar (imagen `54bb35a8`) | Sin calcular | #98 (`develop` a `main`) | dev | Automático (`ci-deploy` 43377137327); actor JulianH2 |
| 2026-11-13 | identity | Sin asignar (imagen `54bb35a8`) | Sin calcular | #98 (`develop` a `main`) | dev | Automático (`ci-deploy` 43377137327); actor JulianH2 |
| 2026-11-13 | messaging | Sin asignar (imagen `54bb35a8`) | Sin calcular | #98 (`develop` a `main`) | dev | Automático (`ci-deploy` 43377137327); actor JulianH2 |
| 2026-11-13 | market | Sin asignar (imagen `54bb35a8`) | Sin calcular | #98 (`develop` a `main`) | dev | Automático (`ci-deploy` 43377137327); actor JulianH2 |

## Cómo leer este registro

- **Versión y puntos:** hasta el 2026-11-26 no se asignó ninguna versión `X.Y.Z`: el repositorio no tiene etiquetas de Git ni releases y las imágenes se etiquetan con el SHA completo del commit. Por eso no se anotan versiones ni se suman puntos hacia atrás.
- **Fechas:** en UTC para las corridas de `ci-deploy`; los despliegues manuales llevan la fecha del registro del equipo. En el del 2026-11-23 de `8ab22b4b` es la fecha en que se comprobó que las cuatro aplicaciones lo ejecutaban.
- **SHA completos:** `cce786524b2a01db25a83936ca48d4d9b7c9fa95`, `9872a0ae07a150bf22dc612f1773b69941cd5858`, `8ab22b4bbbe01810b7df11e0d59314adb8db9def`, `5e7b65c46f05117889bf07ed23f9bf25fddd8da2`, `0ba9f2344305f61bd658a4920064ff351a50a4df` y `54bb35a8ce01a6b1ac183cfb5136e504f2c8555e`.

## Evidencia

| Despliegue | De dónde sale | Qué se comprobó |
|---|---|---|
| Corridas de `ci-deploy` | Registro público de cada corrida en GitHub Actions | La imagen puesta en las cuatro aplicaciones y la prueba de humo en verde |
| `cce78652` | Registro del equipo del 2026-11-26. El CI de ese contenido es la corrida 45837615099 | Cuatro aplicaciones sanas y humo correcto. Los flujos con sesión no se probaron. No se ejecutó `terraform apply` |
| `9872a0ae` y `8ab22b4b` | Registro del equipo del 2026-11-23 | Cuatro aplicaciones sanas y `/health` correcto |

Los despliegues manuales no dejan corrida en GitHub: no se pueden verificar solo con el repositorio. La lista puede estar incompleta para despliegues manuales anteriores o intermedios que no se registraron. La corrida 42542915157 de `ci-deploy` (2026-11-09) publicó imágenes sin actualizar las aplicaciones, así que no figura.

Imagen anterior para volver atrás desde `cce78652`: `9872a0ae`. El registro del 2026-11-23 dice que las cuatro aplicaciones la ejecutaban, pero lista la etiqueta en el registro de contenedores solo para gateway, identity y messaging: **antes de usarla en market hay que comprobar que su imagen existe**. Los datos de `db/` no se revierten con la imagen.
