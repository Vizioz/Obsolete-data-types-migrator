using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;

public static class ContentPickerIdToUdiMigrator
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

    private class ArchetypeConfigRow
    {
        public Guid UniqueID { get; set; }

        public string Value { get; set; }
    }

    public static void MigrateIdsToUdis(ApplicationContext applicationContext)
    {
        var database = applicationContext.DatabaseContext.Database;

        MigrateDataIdsToUdis(database);
        MigrateVortoArchetypeDataIdsToUdis(database);

        LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: republishing all nodes to update xml cache (equivalent to /umbraco/dialogs/republish.aspx?xml=true)");
        var contentService = ApplicationContext.Current.Services.ContentService;
        contentService.RePublishAll();
        umbraco.library.RefreshContent();
        LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: republishing complete");
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
            WHERE cmsDataType.propertyEditorAlias IN ('Umbraco.ContentPicker2')
            AND (dataNvarchar IS NOT NULL OR dataInt IS NOT NULL)
            AND (cmsDocument.published=1 OR cmsDocument.newest=1 OR cmsDocument.updateDate > (SELECT updateDate FROM cmsDocument AS innerDoc WHERE innerDoc.nodeId = cmsDocument.nodeId AND innerDoc.published=1 AND newest=1))
            ORDER BY contentNodeId, cmsDataType.propertyEditorAlias";

        var contentPickerDataToMigrate = database.Query<Row>(sql).ToList();
        if (contentPickerDataToMigrate.Any())
        {
            foreach (var propertyData in contentPickerDataToMigrate)
            {
                int[] ids;

                if (propertyData.dataInt != null)
                {
                    ids = new[] { propertyData.dataInt.Value };
                }
                else if (propertyData.dataNvarchar != null && !propertyData.dataNvarchar.StartsWith("umb://"))
                {
                    ids = propertyData.dataNvarchar.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                }
                else
                {
                    LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) skipping property {propertyData.alias}");
                    continue;
                }

                string csv = string.Join(",", ids);
                Guid[] uniqueIds = null;
                string uniqueIdsCsv = string.Empty;
                if (ids.Any())
                {
                    uniqueIds = database.Query<Guid>($"SELECT uniqueId FROM umbracoNode WHERE id IN ({csv})").ToArray();
                    uniqueIdsCsv = string.Join(",", uniqueIds.Select(id => $"umb://document/{id:N}"));
                }

                LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) converting property {propertyData.alias} from {csv} to {uniqueIdsCsv}");
                database.Execute("UPDATE cmsPropertyData SET dataInt=NULL, dataNvarchar=@0 WHERE id=@1", uniqueIdsCsv, propertyData.id);
            }

            LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis: migrated Umbraco.ContentPicker2 datatypes.");
        }
    }

    private static void MigrateVortoArchetypeDataIdsToUdis(UmbracoDatabase database)
    {
        // Find content picker datatypes
        string contentPickerSql = @"SELECT umbracoNode.uniqueID 
            FROM cmsDataType
            JOIN umbracoNode ON umbracoNode.id = cmsDataType.nodeId
            WHERE cmsDataType.propertyEditorAlias = 'Umbraco.ContentPicker2'";

        var contentPickers = database.Query<Guid>(contentPickerSql).ToList();

        foreach (var contentPicker in contentPickers)
        {
            // Find archetypes using content pickers
            string archetypeSql = $@"SELECT umbracoNode.uniqueID, cmsDataTypePreValues.value
                FROM cmsPropertyType
                JOIN cmsDataType ON cmsDataType.nodeId = cmsPropertyType.dataTypeId
                JOIN cmsDataTypePreValues ON cmsDataTypePreValues.datatypeNodeId = cmsDataType.nodeId AND cmsDataTypePreValues.alias = 'archetypeConfig'
                JOIN umbracoNode ON umbracoNode.id = cmsPropertyType.dataTypeId
                WHERE cmsDataType.propertyEditorAlias IN ('Imulus.Archetype')
                AND cmsDataTypePreValues.value LIKE '%""dataTypeGuid"": ""{contentPicker}""%'";

            var archetypes = database.Query<ArchetypeConfigRow>(archetypeSql).ToList();

            foreach (var archetype in archetypes)
            {
                // Find Vorto properties of archetypes using content pickers
                var sql = $@"SELECT cmsPropertyData.id, cmsPropertyData.contentNodeId, cmsPropertyType.alias, dataNvarchar, dataNtext, dataInt, cmsDocument.*
                    FROM cmsPropertyData
                    JOIN cmsPropertyType ON cmsPropertyType.id = cmsPropertyData.propertytypeid
                    JOIN cmsDataType ON cmsDataType.nodeId = cmsPropertyType.dataTypeId
                    JOIN cmsDataTypePreValues ON cmsDataTypePreValues.datatypeNodeId = cmsDataType.nodeId AND cmsDataTypePreValues.alias = 'dataType'
                    JOIN cmsContentVersion ON cmsContentVersion.VersionId = cmsPropertyData.versionId
                    JOIN umbracoNode ON umbracoNode.id = cmsContentVersion.ContentId
                    JOIN cmsDocument ON cmsDocument.nodeId = umbracoNode.id
                    WHERE cmsDataType.propertyEditorAlias IN ('Our.Umbraco.Vorto')
                    AND cmsDataTypePreValues.value LIKE '%""propertyEditorAlias"": ""Imulus.Archetype""%'
                    AND cmsDataTypePreValues.value LIKE '%""guid"": ""{archetype.UniqueID}""%'
                    AND(dataNtext IS NOT NULL)
                    AND(cmsDocument.published = 1 OR cmsDocument.newest = 1 OR cmsDocument.updateDate > (SELECT updateDate FROM cmsDocument AS innerDoc WHERE innerDoc.nodeId = cmsDocument.nodeId AND innerDoc.published = 1 AND newest = 1))
                    ORDER BY contentNodeId, cmsDataType.propertyEditorAlias";

                var vortoDataToMigrate = database.Query<Row>(sql).ToList();
                var config = JsonConvert.DeserializeObject<Archetype.Models.ArchetypePreValue>(archetype.Value);
                var propertyAliases = config.Fieldsets.SelectMany(fieldset => fieldset.Properties)
                    .Where(property => property.DataTypeGuid == contentPicker)
                    .Select(property => property.Alias);

                if (vortoDataToMigrate.Any())
                {
                    foreach (var propertyData in vortoDataToMigrate)
                    {
                        string udiValue;

                        if (!string.IsNullOrEmpty(propertyData.dataNtext))
                        {
                            // Vorto multilingual values
                            var vortoValue = JsonConvert.DeserializeObject<Our.Umbraco.Vorto.Models.VortoValue>(propertyData.dataNtext);
                            var udiValues = new Dictionary<string, object>();

                            foreach (var value in vortoValue.Values)
                            {
                                // Archetype fieldsets
                                var archetypeValue = JsonConvert.DeserializeObject<Archetype.Models.ArchetypeModel>(value.Value.ToString());
                                var fieldsets = archetypeValue.Fieldsets
                                    .Where(fieldset => fieldset.Properties.Any(property =>
                                        propertyAliases.Contains(property.Alias)));

                                foreach (var fieldset in fieldsets)
                                {
                                    // Properties using content picker
                                    var properties = fieldset.Properties.Where(property =>
                                        propertyAliases.Contains(property.Alias));

                                    foreach (var property in properties)
                                    {
                                        var intValue = property.GetValue<int>();

                                        if (intValue > 0)
                                        {
                                            var uniqueId = database.FirstOrDefault<Guid>(
                                                $"SELECT uniqueId FROM umbracoNode WHERE id = @0", intValue);

                                            property.Value = $"umb://document/{uniqueId:N}";
                                        }
                                        else
                                        {
                                            property.Value = null;
                                        }
                                    }
                                }

                                udiValues[value.Key] = ToDataValue(archetypeValue);
                            }

                            vortoValue.Values = udiValues;
                            udiValue = ToDataValue(vortoValue);
                        }
                        else
                        {
                            LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) skipping property {propertyData.alias} - null dataNtext");
                            continue;
                        }

                        LogHelper.Info(typeof(ContentPickerIdToUdiMigrator), () => $"MigrateIdsToUdis (node id: {propertyData.contentNodeId}) converting property {propertyData.alias} from {propertyData.dataNtext} to {udiValue}");
                        database.Execute("UPDATE cmsPropertyData SET dataNtext=@0 WHERE id=@1", udiValue, propertyData.id);
                    }
                }
            }
        }
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