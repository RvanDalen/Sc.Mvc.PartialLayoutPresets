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
    public class GetAllowedPartialLayoutPresets : GetAllowedRenderings
    {
        protected Dictionary<string, List<ID>> Locations = new Dictionary<string, List<ID>>();
        private Item _contextItem;
        private GetPlaceholderRenderingsArgs _args;
        private List<Item> _placeholderRenderings;
        private string _currentPlaceholder;

        protected const string SharedKey = "shared";

        public virtual void AddLocation(XmlNode configNode)
        {
            var siteName = XmlUtil.GetAttribute("siteName", configNode, true);
            if (string.IsNullOrEmpty(siteName)) siteName = SharedKey;

            var locationId = ID.Parse(XmlUtil.GetAttribute("locationId", configNode, true));

            if (!Locations.ContainsKey(siteName)) Locations.Add(siteName, new List<ID>());
            Locations[siteName].Add(locationId);
        }

        public new void Process(GetPlaceholderRenderingsArgs args)
        {
            _args = args;
            Assert.IsNotNull(args, "args");

            var placeholderKey = _args.PlaceholderKey ?? string.Empty;

            //clean the dynamicplaceholder guids
            placeholderKey = placeholderKey.CleanPlaceholderKey();

            _placeholderRenderings = new List<Item>();
            _currentPlaceholder = placeholderKey.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            //get contextItem from QS if possible
            var queryString = HttpContext.Current.Request.QueryString;
            var itemId = queryString["sc_itemid"] ?? queryString["id"];
            _contextItem = string.IsNullOrEmpty(itemId) ? Context.Item : _args.ContentDatabase.GetItem(itemId);

            //add preset rendering if we are editing a preset
            AddPresetRenderingToBaseTemplate();

            //add normal renderings to presetplaceholder
            AddRenderingsToPresetPlaceholder();

            //add preset definitions if we are editing a page
            AddPresetDefinitionsToPage();

            if (_placeholderRenderings.Count == 0) return;

            if (args.PlaceholderRenderings == null) args.PlaceholderRenderings = new List<Item>();
            args.PlaceholderRenderings.AddRange(_placeholderRenderings);
        }

        private void AddPresetDefinitionsToPage()
        {
            var currentSiteName = GetSiteName(_contextItem);

            //get presets from locations, filter on siteName or shared
            foreach (var location in Locations.Where(location => location.Key.Equals(currentSiteName) || location.Key.Equals(SharedKey))
                                              .SelectMany(location => location.Value))
            {
                var folder = _args.ContentDatabase.GetItem(location);
                if (folder == null) continue;

                foreach (var presetItem in folder.GetChildrenDerivedFrom(Consts.BasePartialLayoutPresetTemplateId))
                {
                    //skip current
                    if (_contextItem != null && presetItem.ID.Equals(_contextItem.ID)) continue;

                    //check if preset placeholder matches the current one
                    var allowedPlaceholder = presetItem.GetPresetPlaceholderFromLayout(_args.DeviceId.ToString());
                    if (string.IsNullOrEmpty(allowedPlaceholder) || !allowedPlaceholder.Equals(_currentPlaceholder, StringComparison.OrdinalIgnoreCase)) continue;

                    _placeholderRenderings.Add(presetItem);
                }
            }
        }

        private void AddRenderingsToPresetPlaceholder()
        {
            if (!_currentPlaceholder.Equals(Consts.PartialLayoutPresetPlaceholderKey)) return;

            var parentPlaceHolder = _args.PlaceholderKey.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                                        .Reverse().Skip(1).FirstOrDefault().CleanPlaceholderKey();


            Item placeholderItem;
            if (ID.IsNullOrEmpty(_args.DeviceId))
            {
                placeholderItem = Client.Page.GetPlaceholderItem(parentPlaceHolder, _args.ContentDatabase, _args.LayoutDefinition);
            }
            else
            {
                using (new DeviceSwitcher(_args.DeviceId, _args.ContentDatabase))
                    placeholderItem = Client.Page.GetPlaceholderItem(parentPlaceHolder, _args.ContentDatabase, _args.LayoutDefinition);
            }
            List<Item> list = null;
            if (placeholderItem != null)
            {
                _args.HasPlaceholderSettings = true;
                bool allowedControlsSpecified;
                list = GetRenderings(placeholderItem, out allowedControlsSpecified);
                if (allowedControlsSpecified)
                {
                    _args.CustomData["allowedControlsSpecified"] = true;
                    _args.Options.ShowTree = false;
                }
            }
            if (list == null) return;
            if (_args.PlaceholderRenderings == null) _args.PlaceholderRenderings = new List<Item>();

            _placeholderRenderings.AddRange(list);
        }

        private void AddPresetRenderingToBaseTemplate()
        {
            if (_contextItem != null &&
                //if the page is a preset page
                _contextItem.IsDerived(Consts.BasePartialLayoutPresetTemplateId) &&
                //if the page does not have a preset component yet
                string.IsNullOrEmpty(_contextItem.GetPresetPlaceholderFromLayout(Context.Device.ID.ToString())))
            {
                var partialLayoutPresetComponent = _args.ContentDatabase.GetItem(Consts.PartialLayoutPresetRenderingId);
                Assert.IsNotNull(partialLayoutPresetComponent, "partialLayoutPresetComponent");

                _placeholderRenderings.Add(partialLayoutPresetComponent);
            }
        }
        
        private string GetSiteName(Item item)
        {
            var siteName = Context.GetSiteName();

            if (item != null)
            {
                //match it with a content site
                foreach (var info in SiteContextFactory.Sites.Where(info => !string.IsNullOrEmpty(info.RootPath) && (info.RootPath != "/sitecore/content" || info.Name.Equals("website"))))
                {
                    if (item.Paths.FullPath.StartsWith(info.RootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        siteName = info.Name;
                        break;
                    }
                }
            }

            return siteName.ToLowerInvariant();
        }
    }
}
