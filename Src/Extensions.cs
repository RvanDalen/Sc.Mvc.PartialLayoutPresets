using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.Layouts;

namespace Sc.Mvc.PartialLayoutPresets
{
    /// <summary>
    /// http://laubplusco.net/sitecore-extensions-does-a-sitecore-item-derive-from-a-template/
    /// </summary>
    public static class Extensions
    {
        //copied from Sitecore.Support.Pipelines.GetPlaceholderRenderings.GetDynamicKeyAllowedRenderings
        private static readonly Regex DynamicPlaceholdersRegex = new Regex("(.+)_[\\d\\w]{8}\\-([\\d\\w]{4}\\-){3}[\\d\\w]{12}");

        public static bool IsDerived([NotNull] this Template template, [NotNull] ID templateId)
        {
            return template.ID == templateId || template.GetBaseTemplates().Any(baseTemplate => IsDerived(baseTemplate, templateId));
        }

        public static bool IsDerived([NotNull] this Item item, [NotNull] ID templateId)
        {
            return TemplateManager.GetTemplate(item).IsDerived(templateId);
        }

        public static IEnumerable<Item> GetChildrenDerivedFrom(this Item item, ID templateId)
        {
            return item.GetChildren().Where(c => c.IsDerived(templateId));
        }

        public static string GetPresetPlaceholderFromLayout(this Item item, string deviceId)
        {
            var presetLayoutXml = LayoutField.GetFieldValue(item.Fields[FieldIDs.FinalLayoutField]);
            var presetDetails = LayoutDefinition.Parse(presetLayoutXml);
            var presetDevice = presetDetails.GetDevice(deviceId);
            if (presetDevice.Renderings == null) return null;

            var allRenderings = presetDevice.Renderings.ToArray().Where(rendering => rendering is RenderingDefinition)
                                                                 .Cast<RenderingDefinition>()
                                                                 .ToList();

            var layoutPresetRendering = allRenderings.FirstOrDefault(rendering => !string.IsNullOrEmpty(rendering.Placeholder) && rendering.Placeholder.EndsWith(Consts.PartialLayoutPresetPlaceholderKey));
            if (layoutPresetRendering == null) return null;
            
            return layoutPresetRendering.Placeholder.CleanPlaceholderKey()
                                                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .LastOrDefault();
        }

        public static string CleanPlaceholderKey(this string placeholderKey)
        {
            var match = DynamicPlaceholdersRegex.Match(placeholderKey);
            if (match.Success && match.Groups.Count > 0)
                placeholderKey = match.Groups[1].Value;

            return placeholderKey;
        }
    }
}
