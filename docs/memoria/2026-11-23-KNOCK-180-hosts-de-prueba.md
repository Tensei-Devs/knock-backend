# 2026-11-23 · KNOCK-180 · Hosts de prueba con una red de confianza válida

## Qué cambió
Todos los hosts de prueba fijan `ForwardedHeaders:KnownNetworks:0 = 10.0.0.0/16` (antes 10.0.0.0/8 o 127.0.0.0/8) y las pruebas del gateway de la spec 035 simulan la IP de conexión 10.0.0.5.

## Por qué
El validador de la spec 035 rechaza redes más amplias que /16 o no privadas fuera de Development; 76 pruebas de integración fallaban al arrancar.

## A evitar
Un host de prueba nuevo debe usar una red privada de /16 o más estrecha; el validador no se relaja.
