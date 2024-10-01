
using TenCrowns.AppCore;
using TenCrowns.GameCore;
using System;

namespace DynamicUnits
{
    public class DynamicUnits: ModEntryPointAdapter
    {
        public override void Initialize(ModSettings modSettings)
        {
            modSettings.Factory = new DynamicUnitsFactory();
        }
    }

    //battlefield promotion
}