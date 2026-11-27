# Pruebas de casos que las specs exigían y faltaban (KNOCK-266)

## Qué cambió
- Pruebas nuevas o ampliadas para 013 CA-07, 056 T-10, 051 CA-01, 11, 14, 17 y 23, y 054 CA-02, 08, 14 y 15. No se tocó `src/`.
- Donde la conducta ya estaba probada (051 CA-11, 054 CA-08 y CA-14) solo se agregó el caso al nombre, con una aserción más en CA-14.
- Columna de estado de los `task.md` de 013, 051, 054 y 056.

## Por qué
`sdd_lint.py --trace` mostraba casos sin prueba y 056 T-10 pedía una prueba de integración propia.

## A evitar
- Marcar un caso en una prueba que no lo comprueba entero: CA-15 exige que la tarea quede `hecha`, y probar solo el manejador no lo demuestra.
- Esperar al TTL de la base dentro de una prueba: se comprueba qué documentos alcanza el índice.
