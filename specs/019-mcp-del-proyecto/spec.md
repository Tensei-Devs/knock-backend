# Spec 019 — MCP del proyecto (backend)

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Requisito del profesor:** RP-72 (establecer MCP al final, en backend y en móvil) y RP-16 (usar MCP donde el servicio externo lo ofrezca). Va al final de la secuencia de specs, como pidió.

## 1. Objetivo
Dejar configurados en el repositorio los servidores MCP que usan los agentes de código del equipo (Claude Code, OpenCode, Copilot y otros) para consultar Jira y, donde aplique, otros servicios, **sin guardar credenciales** y con permisos de solo lectura por defecto.

## 2. Alcance
**Dentro**
- Qué servidores MCP se usan, con qué permisos y para qué.
- Dónde vive la configuración y cómo se autentica cada persona.
- Reglas de seguridad para los agentes que los usan.
- Una página `docs/mcp.md` con la lista de servidores aprobados.

**Fuera**
- Escribir servidores MCP propios.
- Ejecutar olas de cambios en Jira: siguen el flujo de `jira-olas`, con aprobación de una persona.
- Acceso a producción.

## 3. Requisitos
- **R-01:** Jira se consulta con el servidor MCP oficial de Atlassian (remoto, con OAuth). Cada persona se autentica con su propia cuenta; ni el repositorio ni la configuración guardan tokens.
- **R-02:** GitHub se usa con la CLI `gh` ya instalada; no se agrega un servidor MCP de GitHub.
- **R-03:** Si se usa un MCP de MongoDB, apunta solo al contenedor local de desarrollo, en solo lectura; nunca a una cadena de conexión de la nube.
- **R-04:** Los archivos de configuración de MCP no contienen secretos: solo referencias a variables de entorno o el flujo OAuth del servidor.
- **R-05:** Todo servidor MCP nuevo requiere un cambio a esta spec y a `docs/mcp.md` con su propósito, sus permisos y quién lo aprobó.
- **R-06:** Ningún agente usa un MCP para firmar o aprobar una spec, fusionar un PR, cambiar archivos protegidos ni escribir en Jira sin la aprobación de una persona.
- **R-07:** La lectura y la escritura de cada servidor quedan separadas: las herramientas de escritura no están permitidas por defecto en la configuración de los agentes.

## 4. Criterios de aceptación
- **CA-01:** Dada la configuración de MCP del repositorio, cuando se ejecuta la búsqueda de secretos, entonces no encuentra ninguno.
- **CA-02:** Dada una persona sin sesión en Atlassian, cuando un agente intenta consultar Jira, entonces recibe un error de autenticación y no hay un acceso alternativo.
- **CA-03:** Dado un agente con la configuración por defecto, cuando intenta crear o editar una incidencia, entonces la herramienta no está permitida.
- **CA-04:** Dado un servidor que no está en `docs/mcp.md`, cuando se revisa un PR que lo agrega, entonces la revisión lo rechaza.
- **CA-05:** Dada una consulta a Jira por la clave de una incidencia, cuando se hace desde la configuración de cualquiera de los agentes soportados, entonces devuelve el mismo resultado.

## 5. Contrato
No aplica a la API. Interfaces que fija: el nombre del archivo de configuración de cada agente y la tabla de servidores de `docs/mcp.md`.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Un token de Atlassian o de una base de datos se sube al repositorio | Solo OAuth o variables de entorno; búsqueda de secretos en el CI |
| Un agente escribe en Jira o en la base sin que nadie lo vea | Herramientas de escritura no permitidas por defecto; las olas de Jira exigen aprobación |
| Un servidor MCP de terceros extrae datos del proyecto | Solo servidores de la lista aprobada, cada uno con propósito y permisos |
| Instrucciones maliciosas dentro de un resultado de Jira o de la base | Lo que devuelve un MCP es dato, no instrucción: el agente no lo ejecuta |
| El MCP de MongoDB apunta a datos reales | Solo contenedor local y solo lectura |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | No | Es configuración |
| Integración | Sí | Consulta real a Jira con una cuenta de prueba |
| Seguridad negativa | Sí | Secretos en la configuración, escritura no permitida y sesión ausente |
| Contrato | No | No aplica |
| Arquitectura | No | No aplica |

## 8. Decisiones
- **MCP de Atlassian para Jira:** es el que ya usa el equipo en las olas; es remoto y no necesita secretos en el repositorio.
- **`gh` en lugar de un MCP de GitHub:** ya funciona y no agrega superficie.
- **Solo lectura por defecto:** una escritura equivocada en Jira es difícil de revertir.

## 9. Decisiones de la revisión, riesgos y dudas
Decisiones tomadas en la revisión del 2026-11-04. Quien aprueba la spec las acepta; si alguna no convence, se cambia aquí antes de aprobar.

- **Dirección y herramientas del servidor de Atlassian (decidido): se confirman con la documentación oficial al implementar.** No bloquea la aprobación porque la spec fija las reglas (OAuth, sin secretos, solo lectura), no la dirección.
- **MCP de MongoDB (decidido): no se activa en esta etapa.** Si se agrega, cumple R-03.
- **Una sola fuente de configuración (decidido): por ahora un archivo por agente;** se evalúa generarlos con `sync_agents.py` cuando haya más de tres.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
