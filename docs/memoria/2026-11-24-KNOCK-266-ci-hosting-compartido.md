# Spec 055 T-08: los cambios en Knock.Hosting construyen los cuatro servicios

## Qué cambió
- `SHARED` de `ci-pr.yml` y `ci-deploy.yml` incluye `^src/Knock\.Hosting(\.Data)?/`: un cambio en `Knock.Hosting` o `Knock.Hosting.Data` marca gateway, identity, messaging y market como afectados.
- `CODEOWNERS` agrega `/src/Knock.Hosting/` y `/src/Knock.Hosting.Data/`.

## Por qué
- R-15 y CA-10 de la spec 055: los cuatro servicios compilan la biblioteca; sin esto, un cambio solo en ella no disparaba su CI ni su despliegue.

## A evitar
- No volver a acotar `SHARED` a un solo servicio. Si nace otra biblioteca técnica compartida, agregarla a esta expresión en ambos flujos.
