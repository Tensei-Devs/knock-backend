---
name: microservices-gateway
description: Reglas de convivencia entre los tres servicios y la puerta de entrada; úsala al escribir specs de arquitectura o revisar dependencias entre servicios.
---

# microservices-gateway

- Los servicios internos no tienen dirección pública; todo entra por el gateway.
- Ningún servicio lee la base de datos de otro; se piden datos por su interfaz pública.
- Los acuerdos entre servicios viven en una biblioteca de contratos sin lógica.
- Un identificador de correlación viaja en cada petición.
- Cada servicio se construye, prueba y despliega por separado.
