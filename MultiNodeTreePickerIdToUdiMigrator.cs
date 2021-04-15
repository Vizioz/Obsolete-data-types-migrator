using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;

public static class MultiNodeTreePickerIdToUdiMigrator
{
    private class Row
    {
        public int id { get; set; }
        public int contentNodeId { get; set; }
        public string alias { get; set; }
        public string dataNvarchar { get; set; }
        public string dataNtext { get; set; }
        public int? dataInt { get; set; }
        public string preValue { get; set; }
    }

    public static void MigrateIdsToUdis(ApplicationContext applicationContext)
    {
        var database = applicationContext.DatabaseContext.Database;

        string sql = @"SELECT cmsPropertyData.id, cmsPropertyData.contentNodeId, cmsPropertyType.alias, dataNvarchar, dataNtext, dataInt, cmsDataTypePreValues.value as preValue, cmsDocument.*
            FROM cmsPropertyData
            JOIN cmsPropertyType ON cmsPropertyType.id = cmsPropertyData.propertytypeid
            JOIN cmsDataType ON cmsDataType.nodeId = cmsPropertyType.dataTypeId
            JOIN cmsDataTypePreValues ON cmsDataTypePreValues.datatypeNodeId = cmsDataType.nodeId AND cmsDataTypePreValues.alias = 'startNode'
            JOIN cmsContentVersion ON cmsContentVersion.VersionId = cmsPropertyData.versionId
            JOIN umbracoNode ON umbracoNode.id = cmsContentVersion.ContentId
            JOIN cmsDocument ON cmsDocument.nodeId = umbracoNode.id
            WHERE cmsDataType.propertyEditorAlias IN ('Umbraco.MultiNodeTreePicker2')
            AND (dataNvarchar IS NOT NULL OR dataInt IS NOT NULL)
            AND (cmsDocument.published=1 OR cmsDocument.newest=1 OR cmsDocument.updateDate > (SELECT updateDate FROM cmsDocument AS innerDoc WHERE innerDoc.nodeId = cmsDocument.nodeId AND innerDoc.published=1 AND newest=1))
            ORDER BY contentNodeId, cmsDataType.propertyEditorAlias";

        var treePickerDataToMigrate = database.Query<Row>(sql).ToList();
        if (treePickerDataToMigrate.Any())
        {
            foreach (var propertyData in treePickerDataToMigrate)
            {
                int[] ids;

                if (propertyData.dataInt != null)
                {
                    ids = new[] { propertyData.dataInt.Value };
                }
                else if (propertyData.dataNvarchar != null)
                {
                    ids = propertyData.dataNvarchar.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                }
                else
                {
                    LogHelper.Info(typeof(MultiNodeTreePickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) skipping property {propertyData.alias} - null dataInt and null dataNvarchar");
                    continue;
                }

                string csv = string.Join(",", ids);
                var type = propertyData.preValue.Contains("\"type\": \"content\"") ? "document" : "media";
                Guid[] uniqueIds = null;
                string uniqueIdsCsv = string.Empty;
                if (ids.Any())
                {
                    uniqueIds = database.Query<Guid>($"SELECT uniqueId FROM umbracoNode WHERE id IN ({csv})").ToArray();
                    uniqueIdsCsv = string.Join(",", uniqueIds.Select(id => $"umb://{type}/{id:N}"));
                }

                LogHelper.Info(typeof(MultiNodeTreePickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) converting property {propertyData.alias} from {csv} to {uniqueIdsCsv}");
                database.Execute("UPDATE cmsPropertyData SET dataInt=NULL, dataNvarchar=NULL, dataNtext=@0 WHERE id=@1", uniqueIdsCsv, propertyData.id);
            }

            LogHelper.Info(typeof(MultiNodeTreePickerIdToUdiMigrator), () => $"MigrateIdsToUdis: republishing all nodes to update xml cache (equivalent to /umbraco/dialogs/republish.aspx?xml=true)");
            var contentService = ApplicationContext.Current.Services.ContentService;
            contentService.RePublishAll();
            umbraco.library.RefreshContent();
            LogHelper.Info(typeof(MultiNodeTreePickerIdToUdiMigrator), () => $"MigrateIdsToUdis: republishing complete");
        }
    }
}