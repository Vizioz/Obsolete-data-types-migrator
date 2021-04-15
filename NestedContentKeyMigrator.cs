using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Skybrud.Essentials.Json.Extensions;
using Umbraco.Core;
using Umbraco.Core.Logging;

public static class NestedContentKeyMigrator
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

    public static void MigrateKeys(ApplicationContext applicationContext)
    {
        var database = applicationContext.DatabaseContext.Database;

        string sql = @"SELECT cmsPropertyData.id, cmsPropertyData.contentNodeId, cmsPropertyType.alias, dataNvarchar, dataNtext, dataInt, cmsDocument.*
            FROM cmsPropertyData
            JOIN cmsPropertyType ON cmsPropertyType.id = cmsPropertyData.propertytypeid
            JOIN cmsDataType ON cmsDataType.nodeId = cmsPropertyType.dataTypeId
            JOIN cmsContentVersion ON cmsContentVersion.VersionId = cmsPropertyData.versionId
            JOIN umbracoNode ON umbracoNode.id = cmsContentVersion.ContentId
            JOIN cmsDocument ON cmsDocument.nodeId = umbracoNode.id
            WHERE cmsDataType.propertyEditorAlias IN ('Umbraco.NestedContent')
            AND (dataNtext IS NOT NULL AND dataNtext NOT LIKE '%""key"":""%')
            AND(cmsDocument.published = 1 OR cmsDocument.newest = 1 OR cmsDocument.updateDate > (SELECT updateDate FROM cmsDocument AS innerDoc WHERE innerDoc.nodeId = cmsDocument.nodeId AND innerDoc.published = 1 AND newest = 1))
            ORDER BY contentNodeId, cmsDataType.propertyEditorAlias";

        var ncDataToMigrate = database.Query<Row>(sql).ToList();
        if (ncDataToMigrate.Any())
        {
            foreach (var propertyData in ncDataToMigrate)
            {
                if (string.IsNullOrEmpty(propertyData.dataNtext))
                {
                    LogHelper.Info(typeof(NestedContentKeyMigrator), () => $"MigrateKeys (node id: {propertyData.contentNodeId}) skipping property {propertyData.alias} - null dataNtext");
                    continue;
                }

                var ncArray = JArray.Parse(propertyData.dataNtext);
                var ncKeyArray = new JArray();

                foreach (var ncToken in ncArray)
                {
                    var nc = (JObject) ncToken;

                    if (!nc.HasValue("key"))
                    {
                        var key = new JProperty("key", Guid.NewGuid().ToString());
                        nc.AddFirst(key);
                    }

                    ncKeyArray.Add(nc);
                }

                
                var ncKeys = JsonConvert.SerializeObject(ncKeyArray);

                LogHelper.Info(typeof(NestedContentKeyMigrator), () => $"MigrateKeys (node id: {propertyData.contentNodeId}) converting property {propertyData.alias} from {propertyData.dataNtext} to {ncKeys}");
                database.Execute("UPDATE cmsPropertyData SET dataNtext=@0 WHERE id=@1", ncKeys, propertyData.id);
            }

            LogHelper.Info(typeof(NestedContentKeyMigrator), () => $"MigrateKeys: republishing all nodes to update xml cache (equivalent to /umbraco/dialogs/republish.aspx?xml=true)");
            var contentService = ApplicationContext.Current.Services.ContentService;
            contentService.RePublishAll();
            umbraco.library.RefreshContent();
            LogHelper.Info(typeof(NestedContentKeyMigrator), () => $"MigrateKeys: republishing complete");
        }
    }
}