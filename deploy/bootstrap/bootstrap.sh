#!/usr/bin/env bash
# Paso 0 (una sola vez, lo corre una persona con `az login`): crea el almacenamiento del estado de Terraform y la
# identidad federada del pipeline (spec 005, R-03 y R-04). No guarda ni imprime contraseñas.
#
#   SUBSCRIPTION_ID=... GITHUB_REPO=Tensei-Devs/knock-backend ./deploy/bootstrap/bootstrap.sh
#
# Si el tenant universitario no permite registrar aplicaciones, el paso 2 falla: usar el plan B de ADR-003 (regla 5).
set -euo pipefail

: "${SUBSCRIPTION_ID:?Define SUBSCRIPTION_ID}"
: "${GITHUB_REPO:?Define GITHUB_REPO, por ejemplo Tensei-Devs/knock-backend}"
STATE_RG="${STATE_RG:-rg-knock-tfstate}"
STATE_ACCOUNT="${STATE_ACCOUNT:-stknocktfstate}"
STATE_LOCATION="${STATE_LOCATION:-westus}"
APP_RG="${APP_RG:?Define APP_RG, el grupo de recursos donde se despliega Knock}"
GITHUB_ENV_NAME="${GITHUB_ENV_NAME:-dev}"

az account set --subscription "$SUBSCRIPTION_ID"

echo "1/3 Estado remoto"
az group create --name "$STATE_RG" --location "$STATE_LOCATION" --output none
az storage account create --name "$STATE_ACCOUNT" --resource-group "$STATE_RG" --location "$STATE_LOCATION" \
  --sku Standard_LRS --min-tls-version TLS1_2 --allow-blob-public-access false --output none
az storage container create --name tfstate --account-name "$STATE_ACCOUNT" --auth-mode login --output none

echo "2/3 Identidad federada del pipeline (OIDC)"
APP_ID=$(az ad app create --display-name "knock-pipeline" --query appId --output tsv)
az ad sp create --id "$APP_ID" --output none
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"github-${GITHUB_ENV_NAME}\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${GITHUB_REPO}:environment:${GITHUB_ENV_NAME}\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}" --output none

echo "3/3 Roles mínimos"
SP_ID=$(az ad sp show --id "$APP_ID" --query id --output tsv)
RG_ID=$(az group show --name "$APP_RG" --query id --output tsv)
STATE_ID=$(az storage account show --name "$STATE_ACCOUNT" --resource-group "$STATE_RG" --query id --output tsv)
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal --role Contributor --scope "$RG_ID" --output none
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal --role "User Access Administrator" --scope "$RG_ID" --output none
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor" --scope "$STATE_ID" --output none

echo
echo "Guarda estos valores como variables (no secretos) del entorno '${GITHUB_ENV_NAME}' de GitHub:"
echo "  AZURE_CLIENT_ID=$APP_ID"
echo "  AZURE_TENANT_ID=$(az account show --query tenantId --output tsv)"
echo "  AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID"
