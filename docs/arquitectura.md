# Arquitectura del backend

Spec 001 (`specs/001-arquitectura-de-microservicios-y-puerta-de-entrada`). Este documento describe qué servicios existen, qué hace cada uno, cómo entra el tráfico y qué reglas se prueban solas.

## Servicios

| Proyecto | Responsabilidad | Dirección local (desarrollo) |
|---|---|---|
| `Knock.Gateway` | **Única entrada pública.** Enruta cada petición al servicio correcto, reenvía `X-Correlation-Id` y expone `/health`. La validación del token y el límite de peticiones se agregan aquí con la spec 009 | `http://localhost:5198` |
| `Knock.Identity.Api` | Identidad y confianza: registro e inicio de sesión, sesión, perfil y sesiones por dispositivo (specs 015 a 018) | `http://localhost:5062` |
| `Knock.Messaging.Api` | Mensajería y notificaciones | `http://localhost:5226` |
| `Knock.Market.Api` | Mercado: oficios, colonias y solicitudes. El módulo Asistente vive aquí (R-07) | `http://localhost:5234` |
| `Knock.Contracts` | Biblioteca de contratos y piezas comunes sin lógica de negocio (R-04): hoy, las reglas y el middleware de `X-Correlation-Id` | — |

## Cómo entra el tráfico

```
cliente ─▶ Gateway ─┬─▶ Identity   /api/v1/auth/**  /api/v1/me/**
                    ├─▶ Market     /api/v1/oficios/**  /api/v1/colonias/**
                    └─▶ Messaging  /api/v1/mensajes/**  /api/v1/notificaciones/**
```

- Las rutas y las direcciones de los servicios están en la sección `ReverseProxy` de `src/Knock.Gateway/appsettings.json`. En cada entorno se sustituyen con variables, por ejemplo `ReverseProxy__Clusters__identidad__Destinations__principal__Address`.
- Una ruta que no está en la tabla responde 404 y no llega a ningún servicio.
- Los servicios internos **no tienen dirección pública** (R-02): en Azure usan ingreso interno en Container Apps y solo el gateway tiene dirección externa (spec 005).

## Reglas de convivencia (se prueban en `tests/ArchitectureTest`)

1. Ningún servicio referencia el ensamblado ni los tipos de otro servicio (R-03, CA-03).
2. Ningún servicio lee la base de datos de otro: se hablan por REST o por eventos, y cada servicio tiene su propia base lógica (spec 003).
3. `Knock.Contracts` no referencia ningún servicio (R-04).
4. El gateway no referencia ningún servicio: solo enruta (R-02).
5. Cada regla tiene un caso que demuestra que puede fallar.

## Identificador de correlación

`X-Correlation-Id` (R-05): el gateway lo valida (máximo 64 caracteres de `A-Z a-z 0-9 . _ -`), genera uno si falta o no es válido, lo reenvía al servicio, lo devuelve en la respuesta y lo escribe en el contexto de los registros. Es solo para rastrear, nunca para autorizar. La regla y el middleware viven en `Knock.Contracts.Correlation`.

Pendiente de unificar: `Knock.Market.Api` conserva su propia versión del middleware (con integración de Serilog y de la traza de observabilidad); se reemplaza por la de `Knock.Contracts` cuando la spec 011 lo permita.

## Salud

Los cuatro proyectos responden `GET /health` con `200` y `{ "status": "ok", "service": "..." }`. Market conserva además `/health/ready` (MongoDB).

## Qué falta (otras specs)

| Tema | Spec |
|---|---|
| Validación del token y límite de peticiones en el gateway | 009 |
| Dockerfiles y despliegue de cada servicio | 005, 007 |
| Ingreso interno en la nube (CA-04) | 005 |
| Autenticación y sesiones | 015 a 018 |
