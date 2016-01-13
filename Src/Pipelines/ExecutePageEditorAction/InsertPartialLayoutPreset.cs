using System.Collections.Generic;
using System.Linq;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Pipelines;
using Sitecore.Pipelines.ExecutePageEditorAction;

namespace Sc.Mvc.PartialLayoutPresets.Pipelines.ExecutePageEditorAction
{
    public class InsertPartialLayoutPreset : InsertRendering
    {
        private Language _language;
        private Database _contentDatabase;

        public new void Process(PipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            var insertRenderingArgs = args as ExecuteInsertRenderingArgs;
            if (insertRenderingArgs == null) return;
            Assert.IsNotNull(insertRenderingArgs.Device, "device");
            
            var renderingItem = insertRenderingArgs.RenderingItem;
            Assert.IsNotNull(renderingItem, "renderingItem");

            //check if its a preset, if not then revert to normal InsertRendering
            if (!renderingItem.IsDerived(Consts.BasePartialLayoutPresetTemplateId))
            {
                base.Process(args);
                return;
            }

            var placeholderKey = insertRenderingArgs.PlaceholderKey;
            Assert.IsNotNullOrEmpty(placeholderKey, "placeholderKey");

            //save values for reuse in CopyRenderings
            _language = insertRenderingArgs.Language;
            _contentDatabase = insertRenderingArgs.ContentDatabase;

            //get layout part from renderingitem
            var presetLayoutXml = LayoutField.GetFieldValue(renderingItem.Fields[FieldIDs.FinalLayoutField]);
            var presetDetails = LayoutDefinition.Parse(presetLayoutXml);
            var presetDevice = presetDetails.GetDevice(insertRenderingArgs.Device.ID);

            //collect renderings
            var allRenderings = presetDevice.Renderings.ToArray().Where(rendering => rendering is RenderingDefinition)
                                                                 .Cast<RenderingDefinition>()
                                                                 .ToList();

            //get the first layoutPresetRendering
            var layoutPresetRendering = allRenderings.FirstOrDefault(rendering => rendering.Placeholder.EndsWith("partiallayoutpreset"));
            Assert.IsNotNull(layoutPresetRendering, "layoutPresetRendering");

            //copy renderings and set result
            insertRenderingArgs.Result = CopyRenderings(insertRenderingArgs.Device, allRenderings, layoutPresetRendering.Placeholder, placeholderKey);
        }

        private RenderingReference CopyRenderings(DeviceDefinition device, List<RenderingDefinition> renderings, string sourcePlaceholder, string targetPlaceholder)
        {
            RenderingReference returnValue = null;
            var uniqueIdMappings = new Dictionary<string, string>();

            foreach (var renderingDefinition in renderings)
            {
                //only get the renderings that are placed inside the preset placeholder
                if (renderingDefinition.Placeholder.StartsWith(sourcePlaceholder))
                {
                    //create new uniqueId
                    var newUniqueId = ID.NewID.ToString();
                    uniqueIdMappings.Add(renderingDefinition.UniqueId.ToLowerInvariant().Trim('{', '}'), newUniqueId.ToLowerInvariant().Trim('{', '}'));

                    //modify placeholder
                    var originalPlaceholder = renderingDefinition.Placeholder;
                    renderingDefinition.Placeholder = originalPlaceholder.Replace(sourcePlaceholder, targetPlaceholder);
                    renderingDefinition.UniqueId = newUniqueId;

                    //collect new rendering
                    device.AddRendering(renderingDefinition);

                    //save first rendering which we need to return
                    if (returnValue == null && originalPlaceholder.Equals(sourcePlaceholder))
                    {
                        returnValue = new RenderingReference(renderingDefinition, _language, _contentDatabase);
                    }
                }
            }

            //replace new uniqueIds in dynamicplaceholderkeys
            foreach (RenderingDefinition rendering in device.Renderings)
            {
                foreach (var uniqueIdMapping in uniqueIdMappings)
                {
                    rendering.Placeholder = rendering.Placeholder.Replace(uniqueIdMapping.Key, uniqueIdMapping.Value);
                }
            }

            return returnValue;
        }
    }
}
