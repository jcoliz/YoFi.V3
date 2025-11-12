# Provisioning Resource Infrastructure for Production

1. Ensure this repository has been cloned with submodules, or if not, initialize the submodules now
    ```
    git submodule update --init --recursive
    ```
2. Ensure you're logged into Azure using the Azure CLI into the correct subscription where you want the resources provisioned.
    ```
    az login --tenant=<your_tenant_id>
    az account show
    ```
3. Choose a resource group name to provision into. Choose a location for the resource group and most resources. Also choose a resource group for your static web app; note that static web app is only allowed in a small number of regions.
4. Run the provisioning script, providing these values
    ```
    ./infra/Provision-Resources.ps1 -ResourceGroup <your_resource_group> -Location <primary_location> -StaticWebAppLocation <static_web_app_location>
    ```
5. Deploy to the production resources using Azure Pipelines (instructions to follow). Note that you will need some of the outputs of this script as variables for the Azure Pipeline.

## TODO

In the future, this script will be improved to...

* Assign a custom domain to the static web app
* Include storage (when features needing storage get implemented)
