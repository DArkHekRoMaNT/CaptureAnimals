using CommonLib.Config;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;

namespace CaptureAnimals
{
    [Config("captureanimals")]
    public class Config
    {
        private string? _lastExclideEntities = null;
        private string? _lastIncludeEntities = null;
        private readonly HashSet<string> _availableEntities = [];

        [Description("Exclude these entities, support wildcards. Use comma (,) as separator. Take precedence over IncludeEntities")]
        public string ExcludeEntities { get; set; } = string.Empty;


        [Description("Include these entities, support wildcards. Use comma (,) as separator")]
        public string IncludeEntities { get; set; } = "*:*";

        public string[] GetAvailableEntities(ICoreAPI api)
        {
            if (_lastExclideEntities != ExcludeEntities ||
                _lastIncludeEntities != IncludeEntities)
            {
                _lastExclideEntities = ExcludeEntities;
                _lastIncludeEntities = IncludeEntities;
                UpdateAvailableEntities(api);
            }

            return _availableEntities.ToArray();
        }

        // [Subscribe(nameof(this.ExcludeEntities), nameof(this.IncludeEntities))]
        public void UpdateAvailableEntities(ICoreAPI api)
        {
            string[] included = Parse(IncludeEntities);
            string[] excluded = Parse(ExcludeEntities);

            var list = new HashSet<string>();
            foreach (EntityProperties entity in api.World.EntityTypes)
            {
                if (!entity.Server.BehaviorsAsJsonObj.Any(e => e["code"].AsString() == "health"))
                {
                    continue;
                }

                AssetLocation code = entity.Code;
                foreach (string entityName in included)
                {
                    if (WildcardUtil.Match(new(entityName), code))
                    {
                        bool skip = false;

                        foreach (string entityNameExcluded in excluded)
                        {
                            if (WildcardUtil.Match(new(entityNameExcluded), code))
                            {
                                skip = true;
                            }
                        }

                        if (!skip)
                        {
                            list.Add(code.ToString());
                        }
                    }
                }
            }

            _availableEntities.Clear();
            _availableEntities.AddRange(list);

            static string[] Parse(string codes)
            {
                if (string.IsNullOrEmpty(codes))
                {
                    return [];
                }
                return codes.Split(',');
            }
        }
    }
}
