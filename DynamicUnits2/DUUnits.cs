using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using TenCrowns.GameCore;
using UnityEngine;

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
            else if (((DUTiles) pTile).semipassable())
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
            return false; //not water, not mountain
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
            if (((DUTiles) tile()).semipassable())
                pFromTile.getContiguous((Tile tile) => ((DUTiles) tile).semipassable() && tile.distanceTile(pFromTile) <= waterControl(), value);
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
     
        public override bool canMarchEver()
        {
            if (movement() < 0)
                return false;
            return base.canMarchEver();
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
           if (this.game().modSettings().ModPath.IsModLoaded("Morale")) 
              return info().miHPMax;
            else return info().miHPMax + (getLevel() - 1) * 2;//no morale mod activated
        }
         
       public override int attackValue(AttackType eAttack)
       {
           int iValue = 0;
           
           foreach (EffectUnitType eLoopEffectUnit in getEffectUnits())
           {
               int iSubValue = infos().effectUnit(eLoopEffectUnit).maiAttackValue[eAttack];
                if (iSubValue > iValue)
               {
                   iValue = iSubValue; //was +=
               }
           }

           return iValue;
       }

        /// give some bonus xp before actually attacking; COMBAT_BASE_XP is expected to be drastically lowered--xp based on damage done, not kills <summary>
        /// give some bonus xp before actually attacking; COMBAT_BASE_XP is expected to be drastically lowered--xp based on damage done, not kills
        /// </summary>
        /// <param name="pFromTile"></param>
        /// <param name="pToTile"></param>
        /// <param name="bTargetTile"></param>
        /// <param name="iAttackPercent"></param>
        /// <param name="pActingPlayer"></param>
        /// <param name="azTileTexts"></param>
        /// <param name="eOutcome"></param>
        /// <param name="bEvent"></param>
        /// <returns></returns>
        protected override int attackTile(Tile pFromTile, Tile pToTile, bool bTargetTile, int iAttackPercent, Player pActingPlayer, ref List<TileText> azTileTexts, out AttackOutcome eOutcome, ref bool bEvent)
       {
           int xp = -infos().Globals.COMBAT_BASE_XP;
           if (canDamageCity(pToTile))
               xp += infos().Globals.BASE_DAMAGE + attackCityDamage(pFromTile, pToTile.city(), iAttackPercent);
           Unit pDefendingUnit = pToTile.defendingUnit();
         
           if (pDefendingUnit != null && canDamageUnit(pDefendingUnit))
           {
               int estimate = attackUnitDamage(pFromTile, pDefendingUnit, false, iAttackPercent); //no crit, but also unlimited by actual remaining HP
               if (game().isHostileUnit(getTeam(), TribeType.NONE, pDefendingUnit))
               {
                   if (estimate > infos().Globals.BASE_DAMAGE) //good job, above expected
                       xp += pDefendingUnit.getLevel() * 2;
                   xp += estimate + (estimate < pDefendingUnit.getHP() ? 0 : pDefendingUnit.getHPMax()); //either gain xp based on estimate, or a bit more for kill bonus
                  
                   if (bTargetTile)
                   { //main attack, not a AoE
                       xp += info().mbMelee ? pDefendingUnit.modifiedStrength() / 10 : infos().Globals.BASE_DAMAGE;
                   }
                   else
                   {
                       xp *= 150;
                       xp /= 100;
                   }
               }
           }
         
           doXP(xp, ref azTileTexts);
           return base.attackTile(pFromTile, pToTile, bTargetTile, iAttackPercent, pActingPlayer, ref azTileTexts, out eOutcome, ref bEvent);
       }
       
       //ignore tiny xp gains; reduce text notification noises
       protected override void doXP(int multiplier, ref List<TileText> azTileTexts)
       {
           if (multiplier > infos().Globals.BASE_DAMAGE/2) 
               base.doXP(multiplier, ref azTileTexts);
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