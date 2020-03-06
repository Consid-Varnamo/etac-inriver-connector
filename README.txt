Updates in iPMC -> Epi connector

- This is a remake of the on prem Epi connector so most of the documentation on the wiki is still valid (http://wiki.inriver.com/wiki/episerver-commerce-3)
- The settings PUBLISH_FOLDER and PUBLISH_FOLDER_RESOURCES are obsolete and replaced by:
STORAGE_NAME - Name of the cloud file share
STORAGE_KEY - Key to the cloud file share
STORAGE_SHARE_REFERENCE - Name of the start directory in the cloud file share
STORAGE_CATALOG_DIRECTORY_REFERENCE - Name of the directory for the catalog.xml
STORAGE_RESOURCES_DIRECTORY_REFERENCE - Name of the directory for the Resources.

example:
{"STORAGE_NAME", "epiconnectorfileshare"},
{"STORAGE_KEY","+0nvxmkhyksrGep75ppEMZefDSm7K6mtTlJXv0P//j5yYdh/uiE61234fy5sgs456sfw==" },
{"STORAGE_SHARE_REFERENCE", "root"},
{"STORAGE_CATALOG_DIRECTORY_REFERENCE", "catalog"},
{"STORAGE_RESOURCES_DIRECTORY_REFERENCE", "resource"}

- The same keys have been added for the Episerver side. So in the web.config you now need to add:

<add key="inRiver.StorageAccountName" value="epiconnectorfileshare" />
<add key="inRiver.StorageAccountKey" value="+0nvxmkhyksrGep75ppEMZefDSm7K6mtTlJXv0P//j5yYdh/uiE61234fy5sgs456sfw==" />
<add key="inRiver.StorageAccountShareReference" value="root" />
<add key="inRiver.StorageAccountCatalogDirectoryReference" value="catalog" />
<add key="inRiver.StorageAccountResourcesDirectoryReference" value="resource" /> 

