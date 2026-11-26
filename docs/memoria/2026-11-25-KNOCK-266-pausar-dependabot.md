# Dependabot en pausa mientras se cierra el backend

## Qué cambió
- `.github/dependabot.yml`: `open-pull-requests-limit: 0` en los tres ecosistemas. Los PR #315 a #323 se cerraron sin fusionar (autorizado por José).

## Por qué
- Las actualizaciones no forman parte del cierre y generaban PR abiertos sin atender. La pausa aplica a las actualizaciones de versión; las de seguridad se controlan aparte. Solo surte efecto cuando el archivo llega a la rama por defecto (`main`).

## A evitar
- Reactivar sin revisar los saltos mayores (Microsoft.NET.Test.Sdk 18, Scalar.AspNetCore 3) ni la entrada `docker`, que apunta a `/src/Knock.Api`, carpeta que no es de ningún servicio actual. Dependabot no vuelve a proponer una versión cerrada.
