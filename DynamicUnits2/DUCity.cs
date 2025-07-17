using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TenCrowns.GameCore;

namespace DynamicUnits
{
    internal class DUCity : City
    {
        protected override CityProductionYield getNetCityProductionYieldHelper(YieldType eYield)
        {
            var zYields = base.getNetCityProductionYieldHelper(eYield);
            zYields.iProductionOverflow = Math.Max(0, zYields.iProductionOverflow); // don't allow negative overflow

            return zYields;
        }
        public override bool canBuildProject(ProjectType eProject, bool bBuyGoods, bool bTestEnabled = true, bool bTestGoods = true)
        {
            bool baseResult = base.canBuildProject(eProject, bBuyGoods, bTestEnabled, bTestGoods);
            if (!bTestEnabled)
                return baseResult; // if we are testing if we can build the project, we want to use the base implementation.

            var yieldType = infos().project(eProject).meProductionType;

            if (yieldType != YieldType.NONE && getYield(yieldType, BuildType.NONE, 0) <= 0)
            {
                return false; // if we can't produce anything, we can't build the project.
            }

            return baseResult;

        }

        public bool isNegativeYieldforProject(ProjectType eProject)
        {
            // check if the project has a negative yield for the city.
            var project = infos().project(eProject);
            if (project == null)
                return false; // if the project is not found, we assume it's not negative.
            int yieldValue = calculateCurrentYield(project.meProductionType);
            return yieldValue <= 0; // if the yield is negative, we return true.
        }
    }
}