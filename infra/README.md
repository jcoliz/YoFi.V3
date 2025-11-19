# Provisioning Resource Infrastructure for Production

1. Ensure this repository has been cloned with submodules, or if not, initialize the submodules now
    ```
    git submodule update --init --recursive
    ```

2. Ensure you're logged into Azure using the Azure CLI into the correct subscription where you want the resources provisioned.
    ```
    az login --tenant=<your_tenant_id>
    az account set --subscription <your_subscription_id>
    az account show
    ```

3. Choose a resource group name to provision into. Choose a location for the resource group and most resources. Also choose a resource group for your static web app; note that static web app is only allowed in a small number of regions.

4. Run the provisioning script, providing these values
    ```
    ./infra/Provision-Resources.ps1 -ResourceGroup <your_resource_group> -Location <primary_location> -StaticWebAppLocation <static_web_app_location>
    ```

5. Create a CD pipeline in Azure Pipelines. The script will output a set of variables, which you'll need to set as your pipeline variables

## TODO

In the future, this script will be improved to...

* Assign a custom domain to the static web app
* Include storage (when features needing storage get implemented)
