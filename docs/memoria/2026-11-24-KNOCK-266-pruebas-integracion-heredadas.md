# 2026-11-24 · KNOCK-266 · Seis pruebas de integración rojas en sprint-05

- **Qué cambió:** solo pruebas. (1) Spec062FotosPrivadasFlujoTests registra `LimitesImagen` con contadores en memoria, como ya hace Spec062FotosPrivadasHttpTests; el host no trae Redis y `AddLimitesImagen` pedía `IConnectionMultiplexer` (500 en confirmar). (2) Spec043 CA_043_19 siembra los 25 reportes directo en Mongo; crearlos por la API chocaba con el límite de 10 por hora por persona (429). (3) Spec053 CA_053_05 espera que el cursor de la última página no se envíe, porque el DTO omite el campo nulo.
- **Por qué:** causa raíz en las pruebas, no en producción; las aserciones de fondo (20 y 5 elementos, sin repetir, 422 con `limite=51`, archivos en el contenedor correcto) se mantienen.
- **A evitar:** subir los límites de producción o dormir para sortear el 429; pedir `nextCursor` nulo explícito, que contradice la omisión de nulos del resto de listas.
- **Pendiente:** el CI de integración es quien confirma; en local solo compila.
