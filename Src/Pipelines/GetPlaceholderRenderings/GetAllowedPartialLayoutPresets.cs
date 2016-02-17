using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.GetPlaceholderRenderings;
using Sitecore.Sites;
using Sitecore.Xml;

namespace Sc.Mvc.PartialLayoutPresets.Pipelines.GetPlaceholderRenderings
{
    public class GetAllowedPartialLayoutPresets
    {
        protected Dictionary<string, List<ID>> Locations = new Dictionary<string, List<ID>>();
        protected const string SharedKey = "shared";

        public virtual void AddLocation(XmlNode configNode)
        {
            var siteName = XmlUtil.GetAttribute("siteName", configNode, true);
            if (string.IsNullOrEmpty(siteName)) siteName = SharedKey;

            var locationId = ID.Parse(XmlUtil.GetAttribute("locationId", configNode, true));

            if (!Locations.ContainsKey(siteName)) Locations.Add(siteName, new List<ID>());
            Locations[siteName].Add(locationId);
        }

        public virtual void Process(GetPlaceholderRenderingsArgs args)
        {
            Assert.IsNotNull(args, "args");
            var placeholderKey = args.PlaceholderKey ?? string.Empty;

            //clean the dynamicplaceholder guids
            placeholderKey = placeholderKey.CleanPlaceholderKey();

            var result = new List<Item>();
            var currentPlaceholder = placeholderKey.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            var queryString = HttpContext.Current.Request.QueryString;
            var itemId = queryString["sc_itemid"] ?? queryString["id"];
            var contextItem = args.ContentDatabase.GetItem(itemId);

            //add preset rendering if we can resolve the page
            if (contextItem != null &&
                //if the page is a preset page
                contextItem.IsDerived(Consts.BasePartialLayoutPresetTemplateId) &&
                //if the page does not have a preset component yet
                string.IsNullOrEmpty(contextItem.GetPresetPlaceholderFromLayout(Context.Device.ID.ToString())))
            {
                var partialLayoutPresetComponent = args.ContentDatabase.GetItem(Consts.PartialLayoutPresetRenderingId);
                Assert.IsNotNull(partialLayoutPresetComponent, "partialLayoutPresetComponent");

                result.Add(partialLayoutPresetComponent);
            }

            var currentSiteName = GetSiteName(contextItem);

            //get presets from locations, filter on siteName or shared
            foreach (var location in Locations.Where(location => location.Key.Equals(currentSiteName)
                                                              || location.Key.Equals(SharedKey))
                                              .SelectMany(location => location.Value))
            {
                var folder = args.ContentDatabase.GetItem(location);
                if (folder == null) continue;

                foreach (var presetItem in folder.GetChildrenDerivedFrom(Consts.BasePartialLayoutPresetTemplateId))
                {
                    //skip current
                    if (contextItem != null && presetItem.ID.Equals(contextItem.ID)) continue;

                    //check if preset placeholder matches the current one
                    var allowedPlaceholder = presetItem.GetPresetPlaceholderFromLayout(args.DeviceId.ToString());
                    if (string.IsNullOrEmpty(allowedPlaceholder) || !allowedPlaceholder.Equals(currentPlaceholder, StringComparison.OrdinalIgnoreCase)) continue;

                    result.Add(presetItem);
                }
            }

            if (result.Count == 0) return;

            if (args.PlaceholderRenderings == null) args.PlaceholderRenderings = new List<Item>();
            args.PlaceholderRenderings.AddRange(result);
        }

        private string GetSiteName(Item item)
        {
            string siteName = null;

            if (item != null)
            {
                //match it with a content site
                foreach (var info in SiteContextFactory.Sites.Where(info => !string.IsNullOrEmpty(info.RootPath) && (info.RootPath != "/sitecore/content" || info.Name.Equals("website"))))
                {
                    if (item.Paths.FullPath.StartsWith(info.RootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        siteName = info.Name.ToLowerInvariant();
                        break;
                    }
                }
            }

            return siteName;
        }
    }
}
