using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Sitecore;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.GetPlaceholderRenderings;

namespace Sc.Mvc.PartialLayoutPresets.Pipelines.GetPlaceholderRenderings
{
    public class GetAllowedPartialLayoutPresets
    {
        private StringCollection _locations = new StringCollection();
        public IList Locations
        {
            get { return _locations; }
            set { _locations = (StringCollection)value; }
        }

        public void Process(GetPlaceholderRenderingsArgs args)
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

            //get presets from locations
            foreach (var location in _locations)
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
    }
}
