# 2026-11-23 · KNOCK-281 · Compresión de JSON y validación de caché pasan a Knock.Hosting (spec 055, T-18)

## Qué cambió
`JsonCompressionMiddleware` vive en `src/Knock.Hosting/Http` (con `UseKnockJsonCompression(rutasExtra)`); Identidad agrega `/api/v1/panel` a las rutas que nunca comprime (spec 033, R-22) y Mercado no agrega ninguna. `CacheLifetimeOptionsValidator<T>` es la validación única de la vida de una caché en segundos; `CatalogCacheOptionsValidator` y `ProfileCacheOptionsValidator` solo declaran su clave, sus límites y su propiedad. Los contextos de serialización JSON (`MarketJsonSerializerContext` e `IdentityJsonSerializerContext`) siguen en cada servicio, en archivo propio. Se borraron las dos copias del middleware y las dos validaciones. EXC-018 queda acotada al método `Matches` de los controladores de Mercado e Identidad, que no está en el catálogo de la spec 055 (R-04): moverlo pide enmendar el catálogo, así que no se tocó.

## Por qué
Etapa 3 de la spec 055 (R-08): una sola implementación por pieza del catálogo.

## A evitar
- Volver a copiar el middleware: Identidad solo pasa su ruta extra.
- Retirar EXC-018 mientras `Matches` siga duplicado.
