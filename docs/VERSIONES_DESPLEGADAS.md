# Versiones desplegadas

> Registro de cada despliegue (regla 4 de [`VERSIONAMIENTO.md`](VERSIONAMIENTO.md) y R-08 de la spec 012). Una fila por despliegue y componente, con versión, fecha, cambios y puntos. Se llena a mano hasta que el pipeline de despliegue lo haga automático (T-03 de la spec 012, KNOCK-202).
>
> - **Versión:** `X.Y.Z` calculada con `python scripts/ci/version.py bump --current <anterior> --points <N>`; igual a la etiqueta de Git `<componente>-vX.Y.Z` y a la etiqueta de la imagen.
> - **Cambios:** PR incluidos (`#N`). **Puntos:** suma de las etiquetas `puntos-<componente>-N` de esos PR; con 0 puntos no se despliega una versión nueva.

| Fecha | Componente | Versión | Puntos | Cambios | Entorno | Responsable |
|---|---|---|---|---|---|---|
