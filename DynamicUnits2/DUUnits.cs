using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using System.Linq;
using TenCrowns.GameCore;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace DynamicUnits
{
    internal class DUUnits : Unit
    {
        public override int movement()
        {
            return Math.Max(-1, (info().miMovement + movementExtra()));
        }
    
        public override bool canActMove(Player pActingPlayer, int iMoves = 1, bool bAssumeMarch = false, bool bCancelImprovement = false)
        {
            if (movement() < 0)
                return false;
            else
                return base.canActMove(pActingPlayer, iMoves, bAssumeMarch, bCancelImprovement);
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
        protected override int attackTile(Tile pFromTile, Tile pToTile, bool bTargetTile, int iAttackPercent, Player pActingPlayer, List<TileText> azTileTexts, out AttackOutcome eOutcome, ref bool bEvent)
        {
            int xp;
            Unit pDefendingUnit;
            DUXP();
            bool promoted = false;
            // Debug.Log(String.Format("{0}/{1} chance for battlefield promotion", xp, 50 * getEffectUnitCount()));
            if (xp >= randomNext(42 * getEffectUnitCount())) //not fair, but units with too many effects get overwhelming, so let's curb that. 
            {
                //battlefield promotion
                //promote (without adding to unit level) instead of gaining xp. promotion should be the one most useful against the unit you just attacked
                List<PromotionType> goodPool = new List<PromotionType>();
                PromotionType ePromotion;
                for (ePromotion = (PromotionType)0; ePromotion < infos().promotionsNum(); ePromotion++)
                {
                    if (!hasPromotionAvailable(ePromotion) && isPromotionValid(ePromotion))
                    {

                        InfoPromotion promotion = infos().promotion(ePromotion);
                        if (promotion == null)
                            break;
                        //check situations, add suitable ones to goodPool, then pick one at random
                        if (promotion.meEffectUnit != EffectUnitType.NONE)
                        {
                            //check if good; if so, add value to list, otherwise continue
                            var e = infos().effectUnit(promotion.meEffectUnit);

                            if ((e.maiHeightFromModifier.Get(tile().getHeight()) > 0 && randomNext(2) == 0) ||
                                e.maiImprovementToModifier.Get(pToTile.getImprovement()) > 0 ||
                                (e.maiVegetationFromModifier.Get(tile().getVegetation()) > 0 && randomNext(2) == 0) ||
                                (e.miDamagedUsModifier > 0 && isDamaged() && randomNext(4) == 0) ||
                                (e.miFatigueExtra > 0 && isFatigued() && randomNext(3) == 0) ||
                                (e.miHasGeneralModifier > 0 && hasGeneral() && randomNext(3) == 0) ||
                                (e.miHomeModifier > 0 && pFromTile.hasOwner() && getPlayer() == pFromTile.getOwner()) ||
                                (e.miRiverAttackModifier > 0 && pFromTile.isRiverCrossing(pToTile)) ||
                                (e.miUrbanAttackModifier > 0 && pToTile.isUrban()) ||
                                (e.miWaterLandAttackModifier > 0 && pFromTile.isWater() != pToTile.isWater()) ||
                                validAgainstDefender(e, pDefendingUnit, pFromTile) ||
                                (e.miSettlementAttackModifier > 0 && pToTile.hasCity()) ||
                                (e.meClass != EffectUnitClassType.NONE && hasEffectUnitClass(e.meClass))
                                )
                            {
                                goodPool.Add(ePromotion);
                            }
                        }
                    }
                }

                if (goodPool.Count > 0)
                {
                    var bonusPromotion = goodPool[randomNext(goodPool.Count)];
                    addPromotion(bonusPromotion);
                    promoted = true;

                    // Debug.Log("earned a battlefield promotion");

                    if (hasPlayer())
                    {
                        game().sendTileText(new TileText("+ " + HelpText.TEXT(infos().promotion(bonusPromotion).mName) + "!", pFromTile.getID(), getPlayer()));

                        player().pushLogData(() => TextManager.TEXT("TEXT_GAME_UNIT_BATTLEFIELD_PROMOTION", HelpText.buildUnitNameVariable(this, game()), HelpText.buildPromotionLinkVariable(bonusPromotion)), GameLogType.UNIT_CAPTURED, pToTile.getID(), infos().unit(getType()), pFromTile.getID());
                   
                    }
                }
            }
            if (!promoted)
                doXP(xp, azTileTexts);

            return base.attackTile(pFromTile, pToTile, bTargetTile, iAttackPercent, pActingPlayer, azTileTexts, out eOutcome, ref bEvent);

            void DUXP()
            {
                xp = -infos().Globals.COMBAT_BASE_XP;
                if (canDamageCity(pToTile))
                    xp += infos().Globals.BASE_DAMAGE + attackCityDamage(pFromTile, pToTile.city(), bCritical: false, iAttackPercent);
                pDefendingUnit = pToTile.defendingUnit();
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
                            xp += info().mbMelee ? pDefendingUnit.modifiedDefense() / 10 : 3; //melee gets 4-6xp on avg, ranged only 3
                        }
                        else
                        {
                            xp *= 150;
                            xp /= 100;
                        }
                    }
                }
            }
        }

        private bool validAgainstDefender(InfoEffectUnit e, Unit pDefendingUnit, Tile pFromTile)
        {
            //future implementation: against defender's unit traits
            //e.maiUnitTraitModifier and its 3 cousins, loop through all enemy unit's traits
            if (pDefendingUnit == null)
            {
                return false;
            }
            bool valid = (e.miDamagedThemModifier > 0 && pDefendingUnit.isDamaged() && randomNext(5) == 0) || 
                        (e.miFlankingAttackModifier > 0 && pFromTile.flankingAttack(pDefendingUnit, pDefendingUnit.tile())) || 
                        (e.miVsGeneralModifier > 0 && pDefendingUnit.hasGeneral()) ||
                        (e.mbIgnoresDistance && pFromTile.distanceTile(pDefendingUnit.tile()) > 1);
            foreach (var trait in pDefendingUnit.info().maeUnitTrait) {
                if (valid)
                    break;
                if (e.maiUnitTraitModifier[trait] + e.maiUnitTraitModifierAttack[trait] + e.maiUnitTraitModifierDefense[trait] > 0)
                    valid = true;
                if (e.maiUnitTraitModifierMelee[trait] > 0 && info().mbMelee)
                    valid = true;
                }
            return valid;
        }

        protected override void doXP(int multiplier, List<TileText> azTileTexts)
       {
            if (multiplier <= infos().Globals.BASE_DAMAGE / 2) //ignore tiny xp gains; reduce text notification noises. Also means base game attack xp gains are ignored
                return;
         
            else 
               base.doXP(multiplier, azTileTexts);
       }
       
      public override int attackUnitDamage(Tile pFromTile, Unit pToUnit, bool bCritical, int iPercent = 100, int iExistingDamage = -1, bool bCheckOurUnits = true, int iExtraModifier = 0)
      {
           // base.attackUnitDamage
          Tile pToTile = pToUnit.tile();
          int iToUnitHP = iExistingDamage == -1 ? pToUnit.getHP() : Math.Max(0, pToUnit.getHP() - iExistingDamage);
          if (iPercent < 1)
          {
              return ((iToUnitHP > 1) ? 1 : 0);
          }
          else
          {
              iPercent /= (getCooldown() == infos().Globals.ROUT_COOLDOWN) ? 2: 1;
              return base.attackUnitDamage(pFromTile, pToUnit, bCritical, iPercent, iExistingDamage, bCheckOurUnits, iExtraModifier);
              
          }
      }
      
    }
}