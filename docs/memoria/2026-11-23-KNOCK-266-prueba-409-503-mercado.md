# 2026-11-23 · KNOCK-266 · Prueba de contrato: los 409 y 503 de Mercado están declarados

## Qué cambió
- Nueva prueba `MarketConflictAndUnavailableDeclarationTests` en `tests/ContractTest`: lee el código de cada controlador de Mercado y exige que toda acción que escribe un 409 o un 503 (`Status409Conflict`, `Status503ServiceUnavailable`, `Error(409, ...)`, `Rule(503, ...)`) lo declare con `ProducesResponseType` (en la acción, en la clase o en `DocumentedRequestsController`). Los ayudantes privados del controlador cuentan para el archivo completo (basta que una acción o la clase declare el código).
- Revisión ruta por ruta de los 409 y 503 que Mercado devuelve de verdad: todos los que salen de controladores o de resultados de servicio ya estaban declarados (con el contrato de #295). No se cambió ningún atributo ni el contrato.

## Por qué
La nota anterior hablaba de 48 operaciones sin 409 y 62 sin 503, pero ese conteo es de operaciones del contrato que no declaran el código, no de las que lo devuelven. Declarar un 409 o 503 que una ruta no puede dar sería inventar el contrato.

## A evitar
- Declarar un estado que la ruta no devuelve solo para cubrir el conteo.
- Criterio y límite de la prueba: solo ve lo que el controlador escribe; los `DomainException` que lanza un servicio sin pasar por el controlador (por ejemplo los de `QuoteStore`) se revisan a mano al añadir la ruta.
- Los 409 de `DocumentedRequestsController` se heredan por todas sus rutas, incluidas las de lectura; es una decisión previa de documentación conjunta, no se tocó.
