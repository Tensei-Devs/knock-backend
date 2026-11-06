# Pruebas de mutación (Stryker.NET)

## 1. Qué es la mutación

Las pruebas normales responden «¿el código hace lo que esperamos?». La mutación responde otra pregunta: **«si el código tuviera un error, ¿las pruebas se darían cuenta?»**

Stryker toma el código real, le introduce un error pequeño a propósito (un *mutante*: cambiar `>` por `>=`, `&&` por `||`, borrar una línea, vaciar un texto) y vuelve a correr las pruebas:

| Resultado | Qué significa | ¿Es bueno? |
|---|---|---|
| **Killed** (muerto) | Alguna prueba **falló** con el error metido. | Sí: las pruebas protegen esa línea. |
| **Survived** (sobrevive) | **Todas las pruebas pasaron** aunque el código estaba roto. | No: señala una prueba débil. |
| **No coverage** | Ninguna prueba ejecuta esa línea. | No: falta una prueba. |
| **Timeout** | El mutante provocó un ciclo infinito; cuenta como muerto. | Sí. |
| **Compile error / Ignored** | El mutante no compila o quedó fuera del alcance. | No cuenta. |

Importante: **las pruebas deben pasar sobre el código real.** Stryker solo hace fallar las pruebas sobre las copias con error. Si una prueba falla sobre el código sin tocar, la corrida inicial se ensucia y el puntaje no es confiable.

Puntaje = muertos / (muertos + sobrevivientes + sin cobertura); los timeout cuentan como muertos.

## 2. Qué se mide y qué no

Configuración en `stryker-config.json`; herramienta fijada en `.config/dotnet-tools.json` (`dotnet-stryker` **5.0.0**).

- **Código mutado**: los módulos de la tabla de abajo. Se excluyen `obj`, `bin`, `*.g.cs`, `*.Designer.cs`, `Migrations` y `Program.cs`.
- **Pruebas usadas**: solo `tests/UnitTest`. Stryker descubre solo las demás suites de la solución (IntegrationTest necesita contenedores y fallaría en la corrida inicial), por eso se usa el filtro `FullyQualifiedName~Knock.UnitTest`.
- **Umbrales**: `high 80`, `low 60`, `break 0`. Son informativos: `break 0` nunca hace fallar el CI (y el job `mutation-pr` además es `continue-on-error`). Metas de la estrategia (corrida nightly): Identity (sesión) >= 80 %, negocio >= 70 %.
- **Reportes**: `progress`, `html`, `json` y `markdown` en `StrykerOutput/<fecha>/reports/` (carpeta ignorada por git).
- **No se mide**: código compartido de `Hosting`/`Common`, Messaging, y todo lo que solo cubren las pruebas de integración (las clases `*Store` de Mongo, el gateway con Redis). Esos mutantes salen «sin cobertura» aunque haya pruebas de integración, porque aquí no se ejecutan.
- No se usa `ignore-mutations`: no hay una razón documentada para ocultar ningún tipo de mutante.

| Endpoint | Carpeta mutada |
|---|---|
| `POST /auth/firebase` | `src/Knock.Identity.Api/Modules/Sesiones` |
| `POST /auth/refresh` | `src/Knock.Identity.Api/Modules/Sesiones` |
| `POST /auth/logout` | `src/Knock.Identity.Api/Modules/Sesiones` |
| `GET /me` | `src/Knock.Identity.Api/Modules/Usuarios` |
| `PUT /me` | `src/Knock.Identity.Api/Modules/Usuarios` |
| `GET /oficios` | `src/Knock.Market.Api/Modules/Catalog` |
| `GET /colonias/en-punto` | `src/Knock.Market.Api/Modules/Catalog` |
| Seguridad del gateway | `src/Knock.Gateway` (sin `Program.cs`) |

## 3. Cómo correrlo en local (Windows)

Desde la raíz del repositorio, con el SDK 11 (PowerShell):

```powershell
$env:DOTNET_ROOT = "C:\Users\herna\.dotnet-sdk11"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet tool restore
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

La corrida completa es lenta (compila la solución y repite las pruebas por mutante). Para algo rápido, acota los archivos; los patrones llevan `**/` porque son relativos a cada proyecto:

```powershell
dotnet tool run dotnet-stryker -- --config-file stryker-config.json `
  --mutate "**/Knock.Identity.Api/Modules/Sesiones/Services/SessionRefreshService.cs" `
  --mutate "**/Knock.Identity.Api/Modules/Usuarios/Services/ProfileService.cs"
```

Solo lo cambiado respecto a una rama (como hace el CI): añade `--since:origin/sprint-03`.

Notas: la corrida inicial debe terminar sin pruebas fallidas (si Stryker avisa «N tests are failing», revisa el filtro). Los avisos «Failed to load analyzer ... newer compiler» y algunos «compile error» en modo seguro son normales con .NET 11.

## 4. Cómo leer el informe

1. Abre `StrykerOutput/<fecha>/reports/mutation-report.html`: archivos con su puntaje (verde >= 80, amarillo >= 60, rojo menos).
2. Entra a un archivo: cada línea marcada tiene un mutante **sobreviviente** (o sin cobertura). Pasa el cursor para ver qué cambió.
3. Para cada sobreviviente pregunta qué prueba debería haber fallado y qué aserción falta (el valor exacto, el caso límite `<` frente a `<=`); luego vuelve a correr ese archivo.
4. `mutation-report.md` resume por archivo; `mutation-report.json` sirve para herramientas.
5. Algunos sobrevivientes son «equivalentes» (el cambio no altera el comportamiento observable, por ejemplo un texto de log); se pueden aceptar, pero se documenta.

## 5. Verificación local (2026-11-05)

Corrida acotada a `SessionRefreshService.cs`, `ProfileService.cs` y `SecurityHeadersMiddleware.cs` con `tests/UnitTest` (651 pruebas, todas pasan): **47.17 %** global (25 muertos, 14 sobrevivientes, 14 sin cobertura). Es una medición inicial: las pruebas nuevas de seguridad y unitarias deben subirla. Detalle de sobrevivientes en la entrega (`RESUMEN.md`).
