using Mohawk.SystemCore;
using System.Collections.Generic;
using TenCrowns.GameCore;

namespace DynamicUnits
{
    internal class DUTribes : Tribe
    {
        //deep copy, with one change
        public override void doTurn()
        {
            MohawkAssert.Assert(isAlive());

            if (!tribe().mbDiplomacy)
            {
                return;
            }

            updateLeader();

            updateReligion();

            // if (!hasSites() && !(tribe().mbPersistent) && !hasPlayerAlly()) //prevous code checks for allies before deciding to raid and such
            if (!hasSites() && !(tribe().mbPersistent))
            {
                {
                    using (var unitList = CollectionCache.GetListScoped<int>())
                    {
                        List<int> aiUnits = unitList.Value;
                        foreach (int iUnitID in getUnits())
                        {
                            aiUnits.Add(iUnitID);
                        }

                        foreach (int iUnitID in aiUnits)
                        {
                            Unit pLoopUnit = game().unit(iUnitID);
                            if (pLoopUnit != null)
                            {
                                if (game().randomNext(10) == 0)
                                {
                                    City pCity = pLoopUnit.tile().findBestRaidCity(getTribe());

                                    if (pCity != null)
                                    {
                                        pLoopUnit.convertToRaider(pCity.getTeam());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}