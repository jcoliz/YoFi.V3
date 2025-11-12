# Provisioning Resource Infrastucture for Production

1. Ensure this repository has been cloned with submodules, or if not, initialize the submodules now
    ```
    git submodule update --init --recursive
    ```
1. Ensure you're logged into Azure using the Azure CLI into the correct subscription where you want the resources provisioned.
    ```
    az login --tenant=<your_tenant_id>
    az account show
    ```
1. Run the provisioning script
    ```
    ./infra/Provision-Resources.ps1
    ```
2. Deploy to the production resources using Azure Pipelines (instructions to follow)