﻿using Mohawk.SystemCore;
using System.Collections.Generic;
using TenCrowns.GameCore;
using UnityEngine;

namespace DynamicUnits
{
    internal class DUTiles : Tile
    {
        public override void pillageImprovement(Unit pUnit = null)
        {
            
            ImprovementType eImprovement = getImprovement();
            if (isCitySiteAny() && infos().improvement(eImprovement).meBonusImprovement != ImprovementType.NONE)
            {
                clearImprovement();
                setCitySite(CitySiteType.NONE);
                setImprovement(infos().improvement(eImprovement).meBonusImprovement);
            }
            else
                base.pillageImprovement(pUnit);

        }
       
        public override bool impassable()
        {
            if (game().getTurn() < 2 && semipassable()) //at the beginning of the game, mountains are considered impassable--to help map gen
                return true;

            return mpCurrentData.mTileData.isImpassable(infos());
            
        }
        public override bool canUnitPass(UnitType eUnit, PlayerType ePlayer, TribeType eTribe, TeamType eVisibilityTeam, bool bTestHostileBlocking, bool bTestTerritory)
        {
            bool baseResult = base.canUnitPass(eUnit, ePlayer, eTribe, eVisibilityTeam, bTestHostileBlocking, bTestTerritory);
            
            if (!semipassable())
                return baseResult;

            var Helper = (DynamicUnitsInfoHelper)infos().Helpers;
            if (Helper.isMountaineer(eUnit))
            {
                return baseResult;
            }
            else 
            {
                Player player = ((ePlayer != PlayerType.NONE) ? game().player(ePlayer) : null);
                TeamType teamType = player?.getTeam() ?? TeamType.NONE;
                if (teamType == TeamType.NONE)
                {
                    return false;
                }
                if (isWaterControl(teamType, eVisibilityTeam)) //tile is under anchorship
                {
                    return baseResult;
                }
                return false;
            } 
        }
        public override bool canHaveImprovement(ImprovementType eImprovement, TeamType eTeamTerritory = TeamType.NONE, bool bTestEnabled = true, bool bTestAdjacent = true, bool bTestReligion = true, bool bUpgradeImprovement = false, bool bForceImprovement = false)
        {
            if (semipassable())
                return false;
            return base.canHaveImprovement(eImprovement, eTeamTerritory, bTestEnabled, bTestAdjacent, bTestReligion, bUpgradeImprovement, bForceImprovement);

        }
        public bool semipassable()
        {
            return height().miMovementCost > 14;
        }
        public override bool canBothUnitsOccupy(UnitType eUnit, PlayerType ePlayer, Unit pOtherUnit)
        {
            if (pOtherUnit.getPlayer() == ePlayer)
                if (pOtherUnit.movement() < 1 || infos().unit(eUnit).miMovement < 1)
                    return true;
            return base.canBothUnitsOccupy(eUnit, ePlayer, pOtherUnit); 
        }

        public override bool canUnitOccupy(Unit pUnit, TeamType eVisibilityTeam, bool bTestTheirUnits, bool bTestOurUnits, bool bFinalMoveTile)
        {
            if (pUnit.movement() < 1)
                return true;
            if (bTestTheirUnits && hasUnit())
            {
                using (var unitListScoped = CollectionCache.GetListScoped<int>())
                {
                    getAliveUnits(unitListScoped.Value);
                    foreach (int iUnitID in unitListScoped.Value)
                    {
                        Unit pLoopUnit = game().unit(iUnitID);
                        if (pLoopUnit.movement() < 1 && pUnit.getTeam() != pLoopUnit.getTeam())
                            return false;
                    }
                }
            }
            return base.canUnitOccupy(pUnit, eVisibilityTeam, bTestTheirUnits, bTestOurUnits, bFinalMoveTile);
        }
     }
}