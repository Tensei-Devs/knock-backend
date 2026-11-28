# 2026-11-23 · KNOCK-180 · Spec 035: redes de confianza validadas al arrancar, transformación de encabezados en el gateway y Terraform

## Qué cambió
`Knock.Hosting.ForwardedHeaders.ForwardedHeadersValidator` falla al arrancar fuera de `Development` si `ForwardedHeaders:KnownNetworks` y `KnownProxies` están vacíos o si una red no es privada o es más amplia que /16 (IPv4) o /48 (IPv6); el mensaje nombra la llave, no el valor. El gateway agrega `ForwardedHeadersTransform` (YARP): reemplaza `X-Forwarded-For` por la IP resuelta, fija `X-Forwarded-Proto` y quita `X-Forwarded-Host`, `X-Forwarded-Prefix`, `Forwarded` y `X-Original-For`. `container_apps_subnet_cidr` (deploy/apps y módulo compartido) valida el mismo rango. Regla Semgrep `knock-sin-http-logging` con muestra. Pruebas: `Spec035ConfianzaTests`, `Spec035GatewayTests` y `encabezados_reenviados.tftest.hcl`. Los hosts de prueba en `Production` fijan `ForwardedHeaders:KnownNetworks:0`.

## Por qué
Sin redes de confianza todas las personas comparten la IP del proxy en el límite por IP, y un `X-Forwarded-For` falso podía mover de partición. El servicio nunca debe ver encabezados reenviados escritos por el cliente.

## A evitar
`cidrcontains` no existe en Terraform (se usa expresión regular sobre el primer octeto); un host de prueba nuevo en `Production` debe fijar `ForwardedHeaders:KnownNetworks:0` o no arranca; no desactivar el reenviador por defecto de YARP con otra cosa que `UseDefaultForwarded`.
