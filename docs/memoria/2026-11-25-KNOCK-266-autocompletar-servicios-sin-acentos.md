# Autocompletar de técnicos: suma servicios y busca sin distinguir acentos (046 R-08)

## Qué cambió
- `GET /api/v1/tecnicos/autocompletar` sugiere oficios, servicios (`tipo = servicio`, con el `id` del técnico) y negocios, en ese orden y con un máximo de 8.
- Servicios y negocios solo de técnicos visibles, y la búsqueda ignora acentos (patrón construido desde el texto normalizado y escapado).

## Por qué
- R-08 pide oficios, servicios y negocios; faltaban los servicios, y «plomeria» no encontraba un negocio «Plomería». Decisión de José (2026-11-25): corregir el código.
- Sin migración, índice nuevo ni cambio de esquema: `tipo` ya era texto libre en el contrato. La búsqueda de negocios sigue sin índice, como antes; la de servicios lee como máximo 50 coincidencias.

## A evitar
- Dar por cumplida una sugerencia de tres fuentes sin una prueba por cada una.
- Pendiente aparte: `palabrasBusqueda` (spec 027) se lee pero no se encontró código ni script que la escriba.
