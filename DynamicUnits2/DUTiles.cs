using Mohawk.SystemCore;
using System;
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
            
            return base.impassable() && !semipassable();
            
        }
       // public virtual bool canUnitPass(UnitType eUnit, PlayerType ePlayer, TribeType eTribe, TeamType eVisibilityTeam, bool bTestHostileBlocking, bool bTestTerritory, bool bTestImpassable, Unit pIgnoreUnit = null, bool bForceWaterControl = false)
        public override bool canUnitPass(UnitType eUnit, PlayerType ePlayer, TribeType eTribe, TeamType eVisibilityTeam, bool bTestHostileBlocking, bool bTestTerritory, bool bTestImpassable, Unit pIgnoreUnit = null, bool bForceWaterControl = false)
        {
            bool baseResult = base.canUnitPass(eUnit, ePlayer, eTribe, eVisibilityTeam, bTestHostileBlocking, bTestTerritory, bTestImpassable, pIgnoreUnit); //mountain is passable; baseresult allows everyone to pass
            
            
            if (!semipassable()) //normal case, not mountain
                return baseResult;

            var Helper = (DynamicUnitsInfoHelper)infos().Helpers;
            if (Helper.isMountaineer(eUnit))
            {
                return base.canUnitPass(eUnit, ePlayer, eTribe, eVisibilityTeam, bTestHostileBlocking, bTestTerritory, bTestImpassable:false, pIgnoreUnit, bForceWaterControl);
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
        public override bool isWaterTradeEdge(TeamType eTeam, DirectionType eEdge)
        {
            return base.isWaterTradeEdge(eTeam, eEdge) && height().miMovementCost < 12;
        }
      //public virtual bool canHaveImprovement(ImprovementType eImprovement, City pCity = null, TeamType eTeamTerritory = TeamType.NONE, bool bTestTerritory = true, bool bTestEnabled = true, bool bTestAdjacent = true, bool bTestReligion = true, bool bTestResource = true, bool bUpgradeImprovement = false, bool bForceImprovement = false, bool bTestCulture = true, bool bTestImprovement = true, bool bTestTerrain = true)
        public override bool canHaveImprovement(ImprovementType eImprovement, City pCity = null, TeamType eTeamTerritory = TeamType.NONE, bool bTestTerritory = true, bool bTestEnabled = true, bool bTestAdjacent = true, bool bTestReligion = true, bool bTestResource = true, bool bUpgradeImprovement = false, bool bForceImprovement = false, bool bTestCulture = true, bool bTestImprovement = true, bool bTestTerrain = true)
        {
            if (semipassable())
                return false;
            return base.canHaveImprovement(eImprovement, pCity, eTeamTerritory, bTestTerritory, bTestEnabled, bTestAdjacent, bTestReligion, bTestResource, bUpgradeImprovement, bForceImprovement, bTestCulture, bTestImprovement, bTestTerrain);
           // return base.canHaveImprovement(eImprovement, pCity, eTeamTerritory, bTestTerritory, bTestEnabled, bTestAdjacent, bTestReligion, bUpgradeImprovement, bForceImprovement);
        }
        public bool semipassable()
        {
            if (height() == null)
                return false;
            return height().miMovementCost > 14;
        }

        public override bool skipImprovementUnitTurns(TeamType eActiveTeam)
        {
            bool result = base.skipImprovementUnitTurns(eActiveTeam);
            var range = game().tribeLevel().miMaxUnitsRange;
            if (result && countTribeAvailableUnitsRange(getImprovementTribeSite(eActiveTeam), range) < range)
                return false; //if fewer units than the radius of the camp's control, never skip. Useful when camp is in a really cramped place, mini island etc.
            return result;
        }
        protected override bool canBumpUnit(Unit pUnit, Unit pOtherUnit)
        {
            if (pOtherUnit.movement() < 1 || pUnit.movement() < 1) 
                return false; //can't bump stationary units, and stationary units can't bump
            return base.canBumpUnit(pUnit, pOtherUnit); 
        }
        public override bool canBothUnitsOccupy(Unit pUnit, Unit pOtherUnit)
        {
            if (pOtherUnit.getPlayer() == pUnit.getPlayer())
            {
                if (pOtherUnit.movement() < 1 != pUnit.movement() < 1) //XOR
                    return true;
                if (pUnit != pOtherUnit && pUnit.movement() < 1)
                {
                    return false; 
                }
            }
            else
            {
                //different players, we don't want a unit to be able to push a stationary unit
                if (pOtherUnit.movement() < 1 != pUnit.movement() < 1) //XOR
                    return false;
            }
                return base.canBothUnitsOccupy(pUnit, pOtherUnit); 
        }
        public override bool isResourceValid(ResourceType eResource)
        {
            return base.isResourceValid(eResource) && !semipassable();
        }
    }
}