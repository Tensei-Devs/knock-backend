# Rotación de las claves de servicio (spec 056, D-01 y D-02)

Las llamadas internas entre servicios van firmadas con una clave HMAC por emisor (`interno-key-identity`, `interno-key-market`, `avisos-key-identity`, `avisos-key-market`). Se rotan **a mano cada 90 días**; la ejecuta el Tech Lead, que administra Key Vault. La validación vive en `Knock.Hosting.Security.ServiceCredential` y es la misma en Identidad, Mercado y Mensajería.

## Cómo se configura la clave anterior

Cada clave de configuración `<Clave>` (por ejemplo `Security:InternalTokens:IdentityKey`) admite dos valores opcionales:

| Configuración | Contenido |
|---|---|
| `<Clave>:Previous` | La clave anterior, Base64 de al menos 32 bytes. En Key Vault es el secreto `<nombre>-previous` (por ejemplo `interno-key-identity-previous`). |
| `<Clave>:PreviousUntil` | Instante UTC (ISO 8601) hasta el que se acepta la anterior: el momento del paso 3 más 24 horas. Sin esta fecha la anterior **no** vale (falla cerrado). |

Una credencial firmada con la clave vigente se acepta siempre. Una firmada con la anterior solo se acepta antes de `PreviousUntil`; después responde 401.

## Procedimiento de cinco pasos

1. Crear el valor nuevo con un generador criptográfico (32 bytes en Base64) sin imprimirlo ni guardarlo en archivos.
2. En Key Vault, copiar el valor vigente al secreto `<nombre>-previous` y guardar el valor nuevo como versión nueva de `<nombre>`.
3. Desplegar los servicios que **verifican** y los que **firman** con la clave nueva, con `<Clave>:Previous` leída de `<nombre>-previous` y `<Clave>:PreviousUntil` igual a la hora actual más 24 horas.
4. Esperar las 24 horas. Una credencial vive 5 minutos, así que lo firmado con la clave anterior caduca mucho antes; la ventana cubre despliegues escalonados.
5. Retirar la anterior: borrar `<nombre>-previous` (y `PreviousUntil`) y redesplegar. Anotar la fecha para la rotación siguiente.

## Qué no hacer

- No reutilizar una clave entre emisores ni la `jwt-signing-key` de los usuarios.
- No escribir valores de claves en el repositorio, en `terraform.tfvars` ni en mensajes.
- No dejar `<Clave>:Previous` configurada sin fecha: no se acepta y solo confunde.
