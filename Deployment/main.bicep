@description('Specifies the location in which the Azure Storage resources should be deployed, this is limited to East US or West US for current Computer Vision support')
@allowed(
  [
    'eastus'
    'westus'
  ]
)
param location string

@description('Name for the app service, this will be the beginning of the FQDN.')
param website_name string

@description('Account name for the OpenAI deployment')
param openai_account_name string = uniqueString(website_name, 'openai')

@description('OpenAI Deployment name for the model deployment.')
param openai_deployment_name string = 'gpt35'

@description('Storage account name for image uploads and retention.')
param storage_account_name string = uniqueString(website_name, 'storage')

@description('Computer Vision service name.')
param vision_account_name string = uniqueString(website_name, 'vision')

@description('Container that all photos will be uploaded to.')
param upload_container_name string = 'uploads'

var app_service_plan_name = uniqueString(website_name)

resource openai_account 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  kind: 'OpenAI'
  location: location
  name: openai_account_name
  properties: {
    customSubDomainName: openai_account_name
    networkAcls: {
      defaultAction: 'Allow'
      ipRules: []
      virtualNetworkRules: []
    }
    publicNetworkAccess: 'Enabled'
  }
  sku: {
    name: 'S0'
  }
}

resource openai_deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openai_account
  name: openai_deployment_name
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-35-turbo'
      version: '0301'
    }
  }
  sku: {
    capacity: 30
    name: 'Standard'
  }
}

resource vision_account 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  identity: {
    type: 'None'
  }
  kind: 'ComputerVision'
  location: location
  name: vision_account_name
  properties: {
    customSubDomainName: vision_account_name
    networkAcls: {
      defaultAction: 'Allow'
      ipRules: []
      virtualNetworkRules: []
    }
    publicNetworkAccess: 'Enabled'
  }
  sku: {
    name: 'S1'
  }
}

resource storage_account 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storage_account_name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
  resource blob_service 'blobServices@2022-09-01' = {
    name: 'default'
    properties: {
      cors: {
        corsRules: [
          {
            allowedOrigins: [
              'https://${website_name}.azurewebsites.net'
            ]
            allowedMethods: [
              'GET'
              'HEAD'
              'MERGE'
              'POST'
              'OPTIONS'
              'PUT'
              'PATCH'
            ]
            maxAgeInSeconds: 200
            exposedHeaders: [
              '*'
            ]
            allowedHeaders: [
              '*'
            ]
          }
        ]
      }
    }

    resource container 'containers@2022-09-01' = {
      name: upload_container_name
    }
  }
}

resource blob_lifecycle_policy 'Microsoft.Storage/storageAccounts/managementPolicies@2021-02-01' = {
  name: 'default'
  parent: storage_account
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: '${upload_container_name}dailydelete'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterModificationGreaterThan: 1
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                upload_container_name
              ]
            }
          }
        }
      ]
    }
  }
}

resource app_service_plan 'Microsoft.Web/serverfarms@2022-09-01' = {
  kind: 'linux'
  location: location
  name: app_service_plan_name
  properties: {
    elasticScaleEnabled: false
    hyperV: false
    isSpot: false
    isXenon: false
    maximumElasticWorkerCount: 1
    perSiteScaling: false
    reserved: true
    targetWorkerCount: 0
    targetWorkerSizeId: 0
    zoneRedundant: false
  }
  sku: {
    capacity: 1
    family: 'Pv3'
    name: 'P0v3'
    size: 'P0v3'
    tier: 'Premium0V3'
  }
}

resource app_service 'Microsoft.Web/sites@2020-06-01' = {
  name: website_name
  location: location
  properties: {
    serverFarmId: app_service_plan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|docker.io/smcpresalesaccelerators/azure-openai-social-media-generation:latest'
    }
  }
}

resource app_service_config 'Microsoft.Web/sites/config@2020-06-01' = {
  parent: app_service
  name: 'appsettings'
  properties: {
    ASPNETCORE_URLS: 'http://*:5080'
    AZURE_BLOB_STORAGE_CONNECTION_STRING: 'DefaultEndpointsProtocol=https;AccountName=${storage_account.name};AccountKey=${storage_account.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
    AZURE_BLOB_UPLOAD_CONTAINER: upload_container_name
    AZURE_OPENAI_API_KEY: openai_account.listKeys().key1
    AZURE_OPENAI_CHATGPT_DEPLOYMENT: openai_deployment.name
    AZURE_OPENAI_ENDPOINT: openai_account.properties.endpoint
    DOCKER_REGISTRY_SERVER_URL: 'https://ghcr.io'
    VISION_SERVICE_ENDPOINT: vision_account.properties.endpoint
    VISION_SERVICE_KEY: vision_account.listKeys().key1
  }
}
