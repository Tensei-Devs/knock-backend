"""BUG-001 (KNOCK-266): hook de zap-baseline.py que pasa por el proxy de ZAP una lista fija de rutas, con y sin sesión.

El escaneo sigue siendo pasivo: ZAP solo analiza las respuestas. Todo corre contra la pila efímera del runner con datos de
demostración. Las rutas con sesión son GET; los pocos POST son anónimos o con cuerpo vacío, para revisar respuestas de error.
Falla (y con ello falla el paso) si una ruta centinela no responde lo esperado: así un token rechazado o una pila a medias
no pasan como «escaneo limpio». Deja en `dast-rutas.txt` lo que pidió y el estado de cada respuesta, sin cuerpos ni tokens.
"""
import http.client
import json

PUERTO_ZAP = 8099  # el mismo de `-P` en cmd_options (ci-pr.yml)
DESTINO = "http://localhost:8080"
SESIONES = "/zap/wrk/dast-sesion.json"
RESUMEN = "/zap/wrk/dast-rutas.txt"

TECNICO = "ea0000000000000000000001"  # db/seeds/demo/market/tecnicos.json
SOLICITUD = "eb0000000000000000000001"  # db/seeds/demo/market/solicitudes.json
NADA = "000000000000000000000000"
COLONIA = "6adfe980511e3d878359ca07"  # db/seeds/catalog/colonias.json

# Cada una prueba que la sesión ficticia llega de verdad a un servicio (Identidad, Mercado y Mensajería).
CENTINELAS = {
    ("cliente", "/api/v1/me"): 200,
    ("cliente", "/api/v1/me/tecnicos-confianza"): 200,
    ("cliente", "/api/v1/avisos/no-leidos/contador"): 200,
}

ANONIMAS = [
    "/health", "/health/ready",
    "/api/v1/me", "/api/v1/solicitudes", "/api/v1/conversaciones", "/api/v1/avisos",
    "/api/v1/admin/verificaciones", "/api/v1/admin/reportes", "/api/v1/panel/sesion/renovar",
    "/api/v1/oficios", "/api/v1/oficios/ele", "/api/v1/oficios/no-existe",
    "/api/v1/tecnicos", f"/api/v1/tecnicos/{TECNICO}", f"/api/v1/tecnicos/{NADA}",
    "/api/v1/tecnicos/autocompletar?q=demo", "/api/v1/mapa/tecnicos", "/api/v1/explorar",
    "/api/v1/suspension/apelacion", "/ruta-que-no-existe",
]

CLIENTE = [
    "/api/v1/me", "/api/v1/me/sesiones", "/api/v1/me/direcciones", "/api/v1/me/casa-y-auto",
    "/api/v1/me/consentimientos", "/api/v1/me/pendientes", "/api/v1/me/exportaciones/ultima", "/api/v1/me/verificacion",
    "/api/v1/me/bloqueos", "/api/v1/me/tecnicos-confianza", "/api/v1/me/preferencias-avisos", "/api/v1/reportes/me",
    "/api/v1/solicitudes", f"/api/v1/solicitudes/{SOLICITUD}", f"/api/v1/solicitudes/{NADA}",
    f"/api/v1/solicitudes/{SOLICITUD}/cotizaciones", "/api/v1/agenda", f"/api/v1/visitas/{NADA}",
    f"/api/v1/visitas/{NADA}/cotizacion-final", f"/api/v1/visitas/{NADA}/ajustes-pendientes",
    f"/api/v1/busqueda?q=electricista&coloniaId={COLONIA}", "/api/v1/zonas/cp/42800",
    f"/api/v1/colonias/existe?id={COLONIA}&cp=42800", "/api/v1/colonias/en-punto?lat=20.05&lng=-99.34",
    "/api/v1/cotizaciones/referencia-mercado", "/api/v1/asistente/rango-precio",
    f"/api/v1/asistente/bitacora/{SOLICITUD}",
    f"/api/v1/tecnicos/{TECNICO}/servicios", f"/api/v1/tecnicos/{TECNICO}/opiniones",
    f"/api/v1/tecnicos/{TECNICO}/fotos-trabajos", f"/api/v1/tecnicos/{TECNICO}/llega-a-mi-colonia",
    "/api/v1/conversaciones", "/api/v1/conversaciones/no-leidas", f"/api/v1/conversaciones/{NADA}",
    f"/api/v1/conversaciones/{NADA}/mensajes", "/api/v1/avisos", "/api/v1/avisos/no-leidos/contador",
    "/api/v1/admin/verificaciones",
]

TECNICO_RUTAS = [
    "/api/v1/tecnicos/me/solicitudes", "/api/v1/tecnicos/me/cotizaciones", "/api/v1/tecnicos/me/trabajo",
    "/api/v1/tecnicos/me/horarios", "/api/v1/tecnicos/me/reputacion", "/api/v1/agenda",
]

POST = [
    (None, "/api/v1/auth/firebase"), (None, "/api/v1/auth/refresh"), (None, "/api/v1/recuperacion-acceso"),
    (None, "/api/v1/panel/sesion/celular"), ("cliente", "/api/v1/solicitudes"), ("cliente", "/api/v1/oficios-pedidos"),
]


def pedir(metodo, ruta, token):
    conexion = http.client.HTTPConnection("localhost", PUERTO_ZAP, timeout=30)
    encabezados = {"Host": "localhost:8080", "Accept": "application/json"}
    if token:
        encabezados["Authorization"] = f"Bearer {token}"
    cuerpo = None
    if metodo == "POST":
        cuerpo = "{}"
        encabezados["Content-Type"] = "application/json"
    try:
        # URL absoluta: es una petición a través del proxy de ZAP, que la registra y la analiza.
        conexion.request(metodo, DESTINO + ruta, body=cuerpo, headers=encabezados)
        respuesta = conexion.getresponse()
        respuesta.read()
        return respuesta.status
    finally:
        conexion.close()


def recorrer(tokens, pedir=pedir):
    """Devuelve (líneas del resumen, fallos de las centinelas)."""
    plan = [("GET", None, r) for r in ANONIMAS]
    plan += [("GET", "cliente", r) for r in CLIENTE]
    plan += [("GET", "tecnico", r) for r in TECNICO_RUTAS]
    plan += [("POST", quien, r) for quien, r in POST]
    lineas, fallos = [], []
    for metodo, quien, ruta in plan:
        estado = pedir(metodo, ruta, tokens[quien] if quien else None)
        lineas.append(f"{estado} {metodo} {ruta} [{quien or 'sin sesión'}]")
        esperado = CENTINELAS.get((quien, ruta)) if metodo == "GET" else None
        if esperado is not None and estado != esperado:
            fallos.append(f"{ruta}: se esperaba {esperado} y respondió {estado}")
    return lineas, fallos


def zap_spider(zap, target):
    """Hook de ZAP: corre antes de la araña; no devuelve nada para no cambiar sus argumentos."""
    with open(SESIONES, encoding="utf-8") as archivo:
        tokens = json.load(archivo)
    lineas, fallos = recorrer(tokens)
    print("\n".join(lineas))
    print(f"DAST: {len(lineas)} peticiones; {sum(1 for l in lineas if l.startswith('5'))} con respuesta 5xx")
    if fallos:
        # La excepción hace salir a zap-baseline.py con código 3.
        raise RuntimeError("Rutas centinela fallidas: " + "; ".join(fallos))
    with open(RESUMEN, "w", encoding="utf-8") as archivo:
        archivo.write("\n".join(lineas) + "\n")
