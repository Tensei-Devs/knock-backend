# 2026-11-23 · KNOCK-000 · Conciliación de task.md con sprint-05 (tercera pasada)

## Qué cambió
Los `task.md` de las specs 060 y 062 estaban todos en «pendiente» aunque su código y pruebas ya estaban fusionados (PR #294, #332 y #287). La 060 queda con T-01 a T-09 hechas y T-10 parcial (falta el par revisor y el `security-reviewer`); la 062 con T-01 a T-03 hechas, T-04 a T-07 parciales y T-08 pendiente, porque `sdd_lint --trace 062` aún marca seis casos sin prueba con ID. También se corrigió el texto de 053 T-05 y T-06 (ya existe `Spec053ReportesPanelTests`), 055 T-14 (existe `HostingLibraryBoundaryTests.Servicios_Sin_Copias_De_La_Etapa_2_CA_055_11`) , 032 T-08, 049 T-08 (hecha: docs, matriz y rutas del gateway existen) y 050 T-08 y T-12 (consumidor e interna de «no terminado» ya existen; docs hechas).

## Por qué
Un `task.md` desactualizado hace creer que falta trabajo que ya está, y a la inversa. Cada estado cita archivo o prueba.

## A evitar
- Marcar «hecha» una tarea cuyo caso no tiene prueba con su ID: se deja parcial y se nombra lo que falta.
- Dar por pendiente lo que está fusionado sin buscar su prueba y su PR.
