using System.Configuration;
using Umbraco.Core;

internal class StartupHandler : ApplicationEventHandler
{
    protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
    {
        // migrate multi node tree picker ids to udis
        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableMultiNodeTreePickerIdToUdiMigrator"]) && bool.TryParse(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableMultiNodeTreePickerIdToUdiMigrator"], out bool treeEenabled) && treeEenabled)
        {
            MultiNodeTreePickerIdToUdiMigrator.MigrateIdsToUdis(applicationContext);
        }

        // migrate multi url picker ids to udis
        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableMultiUrlPickerIdToUdiMigrator"]) && bool.TryParse(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableMultiUrlPickerIdToUdiMigrator"], out bool urlEnabled) && urlEnabled)
        {
            MultiUrlPickerIdToUdiMigrator.MigrateIdsToUdis(applicationContext);
        }

        // migrate content picker ids to udis
        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableContentPickerIdToUdiMigrator"]) && bool.TryParse(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableContentPickerIdToUdiMigrator"], out bool contentEnabled) && contentEnabled)
        {
            ContentPickerIdToUdiMigrator.MigrateIdsToUdis(applicationContext);
        }
        
        // migrate media picker ids to udis
        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableMediaPickerIdToUdiMigrator"]) && bool.TryParse(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableMediaPickerIdToUdiMigrator"], out bool mediaEnabled) && mediaEnabled)
        {
            MediaPickerIdToUdiMigrator.MigrateIdsToUdis(applicationContext);
        }

        // migrate nested content to include key
        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableNestedContentKeyMigrator"]) && bool.TryParse(ConfigurationManager.AppSettings["Sniper.Umbraco.EnableNestedContentKeyMigrator"], out bool ncEnabled) && ncEnabled)
        {
            NestedContentKeyMigrator.MigrateKeys(applicationContext);
        }
    }
}