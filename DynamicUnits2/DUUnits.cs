using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using TenCrowns.GameCore;

namespace DynamicUnits
{
    internal class DUUnits : Unit
    {
        public override int movement()
        {
            return Math.Max(-1, (info().miMovement + movementExtra()));
        }
     
        public override bool canActMove(Player pActingPlayer, int iMoves = 1, bool bAssumeMarch = false)
        {
            if (movement() < 0)
                return false;
            else
                return base.canActMove(pActingPlayer, iMoves, bAssumeMarch);
        }
        public override bool canAnchor(Player pActingPlayer, Tile pTile, bool bTestEnabled = true)
        {
            if (pTile.isWater())
                return base.canAnchor(pActingPlayer, pTile, bTestEnabled);
            else if (pTile.getHeight() == infos().Globals.MOUNTAIN_HEIGHT)
            {
                //mountaineer code
                if (!(info().mbAnchor))
                {
                    return false;
                }

                if (bTestEnabled)
                {
                    if (isAnchored())
                    {
                        return false;
                    }

                    if (!canUseUnit(pActingPlayer))
                    {
                        return false;
                    }

                    if (!canAct(pActingPlayer, infos().Globals.UNIT_ANCHOR_COST))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false; 
        }
        public override bool isHigherTileDefender(Unit pOtherUnit)
        {
            if (pOtherUnit == null)
            {
                return true;
            }
            
            return tileDefendValue() > ((DUUnits) pOtherUnit).tileDefendValue();
        }
        protected override int tileDefendValue()
        {
            return modifiedDefense() + getHP() * 2;
        }
        //misnomer now; also dubbed for mountain. Using same func to make life easier for AI 
        public override void updateWaterControlTiles(List<Tile> aiWaterControlTiles, Tile pFromTile, TeamType eActiveTeam)
        {
            if (!hasPlayer())
            {
                return;
            }

            ReleasePool<List<int>>.AcquireScope acquireScope = CollectionCache.GetListScoped<int>();
            List<int> value = acquireScope.Value;
            if (tile().getHeight() == infos().Globals.MOUNTAIN_HEIGHT)
                pFromTile.getContiguous((Tile tile) => tile.getHeight() == infos().Globals.MOUNTAIN_HEIGHT && tile.distanceTile(pFromTile) <= waterControl(), value);
            else 
                pFromTile.getContiguous((Tile tile) => tile.isWater() && tile.distanceTile(pFromTile) <= waterControl(), value);
            foreach (int item in value)
            {
                Tile tile2 = game().tile(item);
                if (tile2 != null && tile2.isVisible(eActiveTeam))
                {
                    aiWaterControlTiles.Add(game().tile(item));
                }
            }
        }

        public override bool canSwapUnits(Tile pTile, Player pActingPlayer, bool bMarch)
        {
            if (movement() < 0)
                return false;
            return base.canSwapUnits(pTile, pActingPlayer, bMarch); 
        }

        public override bool hasPush(Tile pToTile)
        {
            if ((pToTile.improvement()?.miDefenseModifier ?? 0) > 30)
                return false;
            if ((pToTile.defendingUnit()?.movement() ?? 0) < 0)
                return false;
            return base.hasPush(pToTile);
        }

        public override int getHPMax()
        {
            return info().miHPMax + (getLevel()-1)*2;
        }
        public override int attackValue(AttackType eAttack)
        {
            int iValue = 0;

            foreach (EffectUnitType eLoopEffectUnit in getEffectUnits())
            {
                int iSubValue = infos().effectUnit(eLoopEffectUnit).maiAttackValue[(int)eAttack];
                if (iSubValue > iValue)
                {
                    iValue = iSubValue;
                }
            }

            return iValue;
        }
        public override int attackUnitDamage(Tile pFromTile, Unit pToUnit, bool bCritical, int iPercent = 100, int iExistingDamage = -1, bool bCheckOurUnits = true, int iExtraModifier = 0)
        {
            Tile pToTile = pToUnit.tile();
            int iToUnitHP = iExistingDamage == -1 ? pToUnit.getHP() : Math.Max(0, pToUnit.getHP() - iExistingDamage);
            if (iPercent < 1)
            {
                return ((iToUnitHP > 1) ? 1 : 0);
            }
            else
            {
                iPercent /= (getCooldown() == infos().Globals.ROUT_COOLDOWN) ? 2: 1;
                int atkStr = attackUnitStrength(pFromTile, pToTile, pToUnit, bCheckOurUnits, iExtraModifier);    
                int defStr = pToUnit.defendUnitStrength(pToTile, this);
            
                int iDamage = infos().Helpers.getAttackDamage(atkStr, defStr, iPercent);
              
                if (bCritical && criticalChanceVs(pToUnit) > 0)
                {
                    iDamage *= 2;
                }

                if (pToUnit.hasLastStand() && (iToUnitHP > 1))
                {
                    if (iDamage >= iToUnitHP)
                    {
                        iDamage = (iToUnitHP - 1);
                    }
                }

                return Math.Min(iDamage, iToUnitHP);
            }
        }

      
    }
}