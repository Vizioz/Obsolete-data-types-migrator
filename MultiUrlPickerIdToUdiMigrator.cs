using System.Collections.Generic;
using System.Linq;
using System.Web.Services.Discovery;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Our.Umbraco.Vorto.Models;
using RJP.MultiUrlPicker.Models;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;

public static class MultiUrlPickerIdToUdiMigrator
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

        MigrateDataIdsToUdis(database);
        MigrateVortoDataIdsToUdis(database);

        LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: republishing all nodes to update xml cache (equivalent to /umbraco/dialogs/republish.aspx?xml=true)");
        var contentService = ApplicationContext.Current.Services.ContentService;
        contentService.RePublishAll();
        umbraco.library.RefreshContent();
        LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: republishing complete");
    }

    private static void MigrateDataIdsToUdis(UmbracoDatabase database)
    {
        string sql = @"SELECT cmsPropertyData.id, cmsPropertyData.contentNodeId, cmsPropertyType.alias, dataNvarchar, dataNtext, dataInt, cmsDocument.*
            FROM cmsPropertyData
            JOIN cmsPropertyType ON cmsPropertyType.id = cmsPropertyData.propertytypeid
            JOIN cmsDataType ON cmsDataType.nodeId = cmsPropertyType.dataTypeId
            JOIN cmsContentVersion ON cmsContentVersion.VersionId = cmsPropertyData.versionId
            JOIN umbracoNode ON umbracoNode.id = cmsContentVersion.ContentId
            JOIN cmsDocument ON cmsDocument.nodeId = umbracoNode.id
            WHERE cmsDataType.propertyEditorAlias IN ('Umbraco.MultiUrlPicker')
            AND (dataNtext IS NOT NULL) AND (dataNtext LIKE '%""id"":%')
            AND (cmsDocument.published=1 OR cmsDocument.newest=1 OR cmsDocument.updateDate > (SELECT updateDate FROM cmsDocument AS innerDoc WHERE innerDoc.nodeId = cmsDocument.nodeId AND innerDoc.published=1 AND newest=1))
            ORDER BY contentNodeId, cmsDataType.propertyEditorAlias";

        var urlPickerDataToMigrate = database.Query<Row>(sql).ToList();
        if (urlPickerDataToMigrate.Any())
        {
            foreach (var propertyData in urlPickerDataToMigrate)
            {
                IEnumerable<Umbraco.Web.Models.Link> multiUrls;

                if (!string.IsNullOrEmpty(propertyData.dataNtext))
                {
                    multiUrls = BuildMultiUrlLinks(propertyData.dataNtext);
                }
                else
                {
                    LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) skipping property {propertyData.alias} - null dataNtext");
                    continue;
                }

                var linksValue = ToDataValue(multiUrls);

                LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) converting property {propertyData.alias} from {propertyData.dataNtext} to {linksValue}");
                database.Execute("UPDATE cmsPropertyData SET dataNtext=@0 WHERE id=@1", linksValue, propertyData.id);
            }

            LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: migrated Umbraco.MultiUrlPicker datatypes.");
        }
    }

    private static void MigrateVortoDataIdsToUdis(UmbracoDatabase database)
    {
        string sql = @"SELECT cmsPropertyData.id, cmsPropertyData.contentNodeId, cmsPropertyType.alias, dataNvarchar, dataNtext, dataInt, cmsDocument.*
            FROM cmsPropertyData
            JOIN cmsPropertyType ON cmsPropertyType.id = cmsPropertyData.propertytypeid
            JOIN cmsDataType ON cmsDataType.nodeId = cmsPropertyType.dataTypeId
            JOIN cmsDataTypePreValues ON cmsDataTypePreValues.datatypeNodeId = cmsDataType.nodeId AND cmsDataTypePreValues.alias = 'dataType'
            JOIN cmsContentVersion ON cmsContentVersion.VersionId = cmsPropertyData.versionId
            JOIN umbracoNode ON umbracoNode.id = cmsContentVersion.ContentId
            JOIN cmsDocument ON cmsDocument.nodeId = umbracoNode.id
            WHERE cmsDataType.propertyEditorAlias IN ('Our.Umbraco.Vorto')
            AND cmsDataTypePreValues.value LIKE '%""propertyEditorAlias"": ""Umbraco.MultiUrlPicker""%'
            AND (dataNtext IS NOT NULL) AND (dataNtext LIKE '%\""id\"":%')
            AND(cmsDocument.published = 1 OR cmsDocument.newest = 1 OR cmsDocument.updateDate > (SELECT updateDate FROM cmsDocument AS innerDoc WHERE innerDoc.nodeId = cmsDocument.nodeId AND innerDoc.published = 1 AND newest = 1))
            ORDER BY contentNodeId, cmsDataType.propertyEditorAlias";
        
        var vortoUrlPickerDataToMigrate = database.Query<Row>(sql).ToList();

        if (vortoUrlPickerDataToMigrate.Any())
        {
            foreach (var propertyData in vortoUrlPickerDataToMigrate)
            {
                string udiValue;

                if (!string.IsNullOrEmpty(propertyData.dataNtext))
                {
                    var vortoValue = JsonConvert.DeserializeObject<Our.Umbraco.Vorto.Models.VortoValue>(propertyData.dataNtext);
                    var udiValues = new Dictionary<string, object>();

                    foreach (var value in vortoValue.Values)
                    {
                        var valueMultiUrls = BuildMultiUrlLinks(value.Value.ToString());
                        udiValues[value.Key] = ToDataValue(valueMultiUrls);
                    }

                    vortoValue.Values = udiValues;
                    udiValue = ToDataValue(vortoValue);
                }
                else
                {
                    LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) skipping property {propertyData.alias} - null dataNtext");
                    continue;
                }

                LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) converting property {propertyData.alias} from {propertyData.dataNtext} to {udiValue}");
                database.Execute("UPDATE cmsPropertyData SET dataNtext=@0 WHERE id=@1", udiValue, propertyData.id);
            }

            LogHelper.Info(typeof(MultiUrlPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: migrated Our.Umbraco.Vorto datatypes containing MultiUrlPicker.");
        }
    }

    private static IEnumerable<Umbraco.Web.Models.Link> BuildMultiUrlLinks(string dataNtext)
    {
        var links = new List<Umbraco.Web.Models.Link>();
        var multiUrls = new RJP.MultiUrlPicker.Models.MultiUrls(dataNtext);

        foreach (var url in multiUrls)
        {
            Udi udi = null;

            if (url.Id != null)
            {
                if (url.Type == LinkType.Media)
                {
                    var media = ApplicationContext.Current.Services.MediaService.GetById(url.Id.Value);
                    udi = new GuidUdi("media", media.Key);
                }
                else if (url.Type == LinkType.Content)
                {
                    var content = ApplicationContext.Current.Services.ContentService.GetById(url.Id.Value);
                    udi = new GuidUdi("document", content.Key);
                }
            }

            var link = new Umbraco.Web.Models.Link { Name = url.Name, Target = url.Target, Udi = udi };
            links.Add(link);
        }

        return links;
    }

    private static string ToDataValue(object obj)
    {
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        settings.Converters.Add(new StringEnumConverter());

        var linksValue = JsonConvert.SerializeObject(obj, settings);

        return linksValue;
    }
}