# Obsolete-data-types-migrator

This project contains a series of utilities classes that help migrating from obsolete datatypes in Umbraco v7 to their newer versions.

Starting from v7.6.0, Umbraco introduced UDI identifiers and started storing identifiers in this new format for most object types.

This is the case for data types using the following property editors:

- Umbraco.ContentPickerAlias -> new editor: Umbraco.ContentPicker2
- Umbraco.MediaPicker -> new editor: Umbraco.MediaPicker2
- Umbraco.MultipleMediaPicker -> new editor: Umbraco.MediaPicker2
- Umbraco.MultiNodeTreePicker -> new editor: Umbraco.MultiNodeTreePicker2
- Our.Umbraco.NestedContent -> new editor: Umbraco.NestedContent
- Umbraco.RelatedLinks -> new editor: Umbraco.RelatedLinks2
- RJP.MultiUrlPicker -> new editor: Umbraco.MultiUrlPicker

When we change our existing obsolete data types in Umbraco to use the new ones, we need to change the stored values of those properties from using the old (id) format to the new (UDI) format.

Starting from this gist https://gist.github.com/kiasyn/bb067269d97a37f76e9a0f8743972837, we wanted to be able to perform an automatic migration for the obsolete data types in our projects. 

The gist contains the code to migrate Umbraco.MultiNodeTreePicker data types. We have used the same approach here and extended it to migrate most of the dataypes above, as well as some complex data types, like Archetype or Vorto properties that contain obsolete media or content pickers within them.

The code hooks into the start up handler and searches for new datatypes using the old data format, updates the values in the published and unpublished documents and republishes the XML cache.

The migration process for each of the datatypes is set on or off by its own key in the appSettings section of the web.config file. So everytime the site restarts, the start up handler will trigger the selected migrations depending on these settings. Each one of specific data type migrations is defined in a separate independent class.

## Data type migrations

The data type migrations included within this project are the following:

### Umbraco.MultiNodeTreePicker

New editor: Umbraco.MultiNodeTreePicker2

- It migrates Multinode Tree Picker properties.

### RJP.MultiUrlPicker

New editor: Umbraco.MultiUrlPicker

- It migrates Multi URL Picker properties.

- It also migrates Vorto properties wrapping a Multi URL Picker, so the values for each variant of the Vorto property are changed from using Ids to using UDIs.

### Umbraco.ContentPickerAlias

New editor: Umbraco.ContentPicker2

- It migrates Content Picker properties.

- It also migrates Vorto properties wrapping an Archetype which contains content pickers, so the values of the archetypes for each variant of the Vorto property are changed from using Ids to using UDIs.

### Umbraco.MediaPicker

New editor: Umbraco.MediaPicker2

- It migrates Media Picker properties, including multiple media pickers.

- It also migrates Vorto properties wrapping an Archetype which contains media pickers, so the values of the archetypes for each variant of the Vorto property are changed from using Ids to using UDIs.

- It also migrates Nested Content properties that contains doctypes with media pickers, so the values of the pickers are changed from using Ids to using UDIs.

### Our.Umbraco.NestedContent 

New editor: Umbraco.NestedContent

- It migrates Content Picker properties, adding a "key" value to each one of the contents.

## How to run the migrations

1. Change the obsolete data types you want to migrate to use the new editors as described in the list above.

2. Add the following settings to the web.config file, setting them to "true" for whichever migration you would like to run:

    ````xml
    <add key="Sniper.Umbraco.EnableMultiNodeTreePickerIdToUdiMigrator" value="true"/>
    <add key="Sniper.Umbraco.EnableMultiUrlPickerIdToUdiMigrator" value="true"/>
    <add key="Sniper.Umbraco.EnableContentPickerIdToUdiMigrator" value="true"/>
    <add key="Sniper.Umbraco.EnableMediaPickerIdToUdiMigrator" value="true"/>
    <add key="Sniper.Umbraco.EnableNestedContentKeyMigrator" value="true"/>
    ````

3. Restart the site or recycle the app pool.

4. The code will run the specified migrations.

5. If the values seem not to be updated, you might need to manually hit a republish at /umbraco/dialogs/republish.aspx?xml=true

6. After the migration is complete, you can turn the app settings values to "false". 
