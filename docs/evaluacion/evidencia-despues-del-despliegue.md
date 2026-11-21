# Evidencia después del despliegue

Lista para correr **apenas haya un SHA final desplegado** (después del paso 3 y de `ci-deploy` en verde). Las capturas con `az` y `terraform` las corre una persona con su sesión de Azure; el generador y las consultas de GitHub se corren con `gh`. Guarda cada salida en `docs/evaluacion/generado/` con la fecha y el SHA.

## 1. Fijar el SHA
```
git fetch origin
git rev-parse origin/main                                   # SHA final que se evalúa
gh run list --workflow ci-deploy --branch main --limit 3    # el último debe estar en «success»
```

## 2. Regenerar las cifras (sin credenciales de Azure)
```
git switch main && git pull
python docs/evaluacion/generar_evidencia.py --gh --sonar
```
Escribe `generado/cifras.json` y `generado/cifras.md` con la fecha y el SHA. El quality gate de SonarCloud solo tendrá valor después del primer análisis de `main` (antes figura como `NONE`).

## 3. Capturas del estado desplegado (las corre quien tenga sesión de Azure)
```
az account show --query "{suscripcion:name, id:id}" -o table
az containerapp list -g <RG> --query "[].{app:name, revision:properties.latestRevisionName, estado:properties.runningStatus, fqdn:properties.configuration.ingress.fqdn}" -o table
for app in knock-gateway knock-identity knock-messaging knock-market; do az containerapp replica list -g <RG> -n $app -o table; done
az containerapp revision list -g <RG> -n knock-market --query "[].{revision:name, activa:properties.active, creada:properties.createdTime, trafico:properties.trafficWeight}" -o table
(cd deploy/apps && terraform output)
(cd deploy/infra && terraform output)
```
Esto contesta «instancias del backend en la nube» con el estado real y no con `evaluacion.tfvars`.

## 4. Pruebas de humo y TLS
```
curl -sS -o /dev/null -w "%{http_code}\n" https://<fqdn-del-gateway>/health
curl -sS -o /dev/null -w "%{http_code}\n" https://<fqdn-del-gateway>/health/ready
curl -sSI https://<fqdn-del-gateway>/api/v1/oficios | head -n 12        # cabeceras de seguridad y ETag
curl -sSv https://<fqdn-del-gateway>/health 2>&1 | grep -iE "SSL connection|TLSv|subject:|expire date"
```

## 5. Evidencia de CI y seguridad (solo lectura)
```
gh run list --workflow ci-pr --branch main --limit 3
gh run list --workflow ci-deploy --limit 5
gh api repos/Tensei-Devs/knock-backend --jq '.security_and_analysis'
gh api repos/Tensei-Devs/knock-backend/rulesets --jq 'length'
gh api repos/Tensei-Devs/knock-backend/code-scanning/alerts --jq 'length'
gh api repos/Tensei-Devs/knock-backend/dependabot/alerts --jq 'length'
```

## 6. Reconciliar y registrar
1. Copia las cifras de `generado/cifras.md` a `docs/EVALUACION.md` (no a mano: pega la tabla generada).
2. Anota en `brechas-conocidas.md` lo que cambió desde la lista anterior.
3. Si algún dato depende de una captura que no se pudo tomar, déjalo escrito como «sin verificar en vivo».
