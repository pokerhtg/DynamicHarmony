using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using TenCrowns.GameCore;
using UnityEngine;

namespace DynamicUnits
{
    internal class DynamicUnitsPlayerAI : Player.PlayerAI
    {
        private int maxDistanceALlowed = 10;

        protected int offset =>  player == null || !player.hasCapitalCity() ? 5: (player.capitalCity().tile().getX() + player.capitalCity().tile().getY() + player.getCapitalCityID()) % 8 - 2; //-2 to 5
        protected int offset2 => player == null || !player.hasCapitalCity() ? 2: (3 * player.capitalCity().tile().getX() + 5 * player.capitalCity().tile().getY() + player.getCapitalCityID()) % 8 - 2;  //-2 to 5, but different from offset to avoid same value in some cases
        protected int gameTurn => game == null? 0 : game.getTurn();
        protected int vp2Win => game == null || player == null || player.getTeam() == TeamType.NONE || !game.areVictoryPointsRelevant() ? 200: game.getVPToWin() - game.countTeamVPs(player.getTeam());
       // protected override int AI_MAX_WATER_CONTROL_DISTANCE => 15;
        protected override int AI_CULTURE_VALUE => base.AI_CULTURE_VALUE + 5 * offset + gameTurn/10; //100 ish
        protected override int AI_HAPPINESS_VALUE => Math.Max(5, (base.AI_HAPPINESS_VALUE - gameTurn/8 * (3 + offset)) //gets less important as game turn ticks on, for most (positive offset) AIs
                                                            * (vp2Win < 100? 2 * vp2Win: 100) / 100); //VP2Win % modifier
        protected override int AI_ORDERS_VALUE =>  base.AI_ORDERS_VALUE + 20 * offset;
        protected override int AI_MONEY_VALUE => Math.Max(5, base.AI_MONEY_VALUE  + offset + offset2 - gameTurn/15); //money worth less overtime, xml states 10
        protected override int AI_TRAINING_VALUE => base.AI_TRAINING_VALUE - offset;
        protected override int AI_GOODS_VALUE => AI_MONEY_VALUE * 4 + base.AI_MONEY_VALUE + offset;
        protected override int AI_MONEY_STOCKPILE_TURNS => base.AI_MONEY_STOCKPILE_TURNS + offset;
        protected override int AI_NUM_GOODS_TARGET => base.AI_NUM_GOODS_TARGET + 300 * offset2; //3000 from xml
        protected override int AI_UNIT_SETTLER_VALUE => Math.Max(base.AI_UNIT_SETTLER_VALUE / 3, base.AI_UNIT_SETTLER_VALUE - 10 * offset * gameTurn * gameTurn); //500,000, so it's valuing less as game ticks on
        protected override int AI_NO_WONDER_TURNS => base.AI_NO_WONDER_TURNS - 2 * offset2;
        protected override int AI_SCIENCE_VALUE => base.AI_SCIENCE_VALUE - gameTurn > 50? gameTurn/2 : 0; //300 from xml. After 50 turns, gradually reduce science value
        protected override int AI_VP_VALUE => (base.AI_VP_VALUE - 20 * (5 + offset) + gameTurn * (gameTurn - 9) / 20) * (vp2Win < 25 ? (400 - 12 * vp2Win): 100) /100; 
        //800 from xml; each player gets an offset that lowers importance, but also gets more interested in VP as game turns tick on, and gets up to 50% more interested in VP as they close in on a VP victory
        protected override int AI_UNIT_GENERAL_VALUE => base.AI_UNIT_GENERAL_VALUE + 20 * offset;

        protected override int AI_YIELD_TURNS => Math.Max(5, base.AI_YIELD_TURNS + offset  //100 is the default
                                    - Math.Max(gameTurn / 5, (80 - 4 * vp2Win)));// //AI presumes fewer turns of game left if close to winning or as game turn ticks on, whichever is bigger
        protected override int AI_UNIT_RANDOM_PROMOTION_VALUE => base.AI_UNIT_PROMOTE_VALUE/3*2;

        protected override int AI_TRADE_NETWORK_VALUE_ESTIMATE => base.AI_TRADE_NETWORK_VALUE_ESTIMATE * (26 + offset) / 26;
        protected override int AI_BUILD_URBAN_VALUE => base.AI_BUILD_URBAN_VALUE + 3 * offset2;
      
        protected override int AI_MAX_FORT_RIVAL_BORDER_DISTANCE => base.AI_MAX_FORT_RIVAL_BORDER_DISTANCE + (12-offset);
        protected override int AI_MAX_FORT_BORDER_DISTANCE_OUTSIDE => base.AI_MAX_FORT_BORDER_DISTANCE_OUTSIDE + (2+offset)/3;
        

        protected override bool isFoundCitySafe(Tile pTile)
        {
            if (getDistanceFromNationBorder(pTile) < infos.Globals.MIN_CITY_SITE_DISTANCE)
                return true;
            else return base.isFoundCitySafe(pTile);
        }
        public override bool isFort(ImprovementType eImprovement)
        {
           return base.isFort(eImprovement) || (infos.improvement(eImprovement).miVisionChange > 1 && infos.improvement(eImprovement).miDefenseModifierFriendly > 0); //if a defensive structure lets you see 2 or more tiles away, it's a fort
        }
        public override long getFortValue(ImprovementType eImprovement, Tile pTile)
        {
            //defense structures aren't that important
            int adjBoost = infos.improvement(eImprovement).maiUnitDie.Count > 0? 80: -30;
            for (DirectionType directionType = DirectionType.NW; directionType < DirectionType.NUM_TYPES; directionType++)
            {
                Tile tile2 = pTile.tileAdjacent(directionType);
                if (tile2 == null)
                {
                    continue;
                }
              
                if (tile2.hasImprovement() && infos.improvement(eImprovement).maiAdjacentImprovementModifier[tile2.getImprovement()] > 0)
                    adjBoost += 50;
            }
            return base.getFortValue(eImprovement, pTile) / 2 + adjBoost;  
        }
        protected override long getImprovementEnableValue(ImprovementType eImprovement, bool bTestTech, bool bTestLaws, bool bTestEffect)
        {
            //way too much value for improvements enablement in base game. AI can't build most of the improvements enabled anyway; too many choices available
            return base.getImprovementEnableValue(eImprovement, bTestTech, bTestLaws, bTestEffect) / 10; 
        }
        public override long getLegitimacyValue(int iLegitimacyChange)
        {
            return base.getLegitimacyValue(iLegitimacyChange) * (120 + offset * 5 - game.getTurn()/5) / 100;  //legitimacy value goes down as game goes on
        }
        protected override long effectPlayerValue(EffectPlayerType eEffectPlayer, ReligionType eStateReligion, bool bRemove)
        {
            long baseValue = base.effectPlayerValue(eEffectPlayer, eStateReligion, bRemove);
            if (!player.isHuman() && !game.isGameOption(infos.Globals.GAMEOPTION_PLAY_TO_WIN)) //not play to win
            {
                int iSubValue = infos.effectPlayer(eEffectPlayer).miVP;
                if (iSubValue != 0)
                {
                    baseValue += (iSubValue * AI_VP_VALUE); //we add in the AI VP value anyway, just like the base game would for play to win situation
                }
            }
            foreach (YieldType eLoopYield in infos.effectPlayer(eEffectPlayer).maeBuyTile)
            {
                baseValue -= 99 * AI_TILE_VALUE * (getCities().Count + 10); // undoing 99% of base game's crazy evaluation of tile buying powers. this is assuming (true as of 4/20/25) base game has the same code, except positive 100 as multiplier
            }

            return baseValue;
        }

        protected override long calculateUnitValue(UnitType eUnit)
        {
            long val = base.calculateUnitValue(eUnit);
            var u = infos.unit(eUnit);
            //inmobile units worth less
            if (u.miMovement < 1)
                val /= 4;
            //navy isn't as important as their land counterparts
            if (isWarship(eUnit))
                val /= 2;
            //can't attack, can't settle, can't build...so weak.
            if (!infos.Helpers.canDamage(eUnit) && !isSettler(eUnit) && !u.mbBuild)
                val /= Math.Max(1, 6 - u.miMovement); //the slower you are, the less you are worth
            return val;
            
        }

        protected override void modifyImprovementValue(ImprovementType eImprovement, Tile pTile, City pCity, ref long iValue)
        {
            base.modifyImprovementValue(eImprovement, pTile, pCity, ref iValue);
            
            if (pTile.getTribeSettlementOrRuins() != TribeType.NONE && eImprovement != pTile.getImprovement()) //encourage building over existing tribal structures
            {   
                iValue += 10 * AI_EXISTING_IMPROVEMENT_VALUE;
            }
            var impv = infos.improvement(eImprovement);
            if (impv.mbWonder)
            {
                var boost = getLegitimacyValue(15 + 6 * (int)impv.meCulturePrereq); 
              //  Debug.Log("value of " + iValue + " getting " + boost);
                iValue += boost; //estimate that each wonder will provide some legitimacy, from cognomen and such
            }
            if (pTile.hasResource() && isImprovementGoodFit(eImprovement, pTile))
            {
                iValue += AI_FAMILY_OPINION_VALUE * 20; //if the reousrce is a lux, it will provide at least 20 opinion worth of family opinion. otherwise it'd be closer to zero, so we'll average estimate it to be 30
            }
        }
        
        protected override int getExtraYieldStockpileNeededWhole(YieldType eYield)
        {
            int baseValue = base.getExtraYieldStockpileNeededWhole(eYield);
            int patience = offset + 5;
          //  Debug.Log(player.getNation().SafeToString()+ " "  + offset + ", " + offset2);
            
            if (game.getTurn() > patience && eYield != infos.Globals.ORDERS_YIELD && game.infos().yield(eYield).mbCanBuy)
            {
                var priceReference = game.getTurnYieldPriceHistory(game.getTurn() - patience, eYield);
                if (priceReference == 0)
                    return baseValue;

                var currPrice = game.getYieldBuyPrice(eYield);
                var shorTermPriceDelta = currPrice - priceReference;
               
                if (shorTermPriceDelta > 0.1 * patience * priceReference) //if the price of a yield has gone up a lot in the last few turns, we want to stockpile more of it
                {
                    baseValue += 300; //if price is jumping, let's stock 300 extra for rainy days
                }
                var originalPrice = infos.yield(eYield).miPrice * Constants.YIELDS_MULTIPLIER;
                if (originalPrice > 0)
                {
                   
                    if (currPrice < originalPrice)
                    {
                        var percentExtra = 100 * 2 * (originalPrice - currPrice) / originalPrice;

                        baseValue = baseValue * (100 + percentExtra) / 100;
                        //if it's cheap, let's stock up extra for future needs. If the price were to hit true min, then we'd stock 100% more
                    }
                }
            }
          
            return baseValue;
        }
      
        public override long calculateYieldValue(YieldType eYield, int iExtraStockpile, int iExtraRate)
        {
            int generalMultiplier = Math.Max(1, offset2 + (int) eYield % Math.Max(1, (offset + 5) / 3)) % 5; //0-4 at the end; and an AI can have different number of yields prioritized (could be % 1, so every yield is different, or at most 4 yields have the same iExtraRate modifier)
            var expectedPrice = infos.yield(eYield).miPrice * Constants.YIELDS_MULTIPLIER;
            if (expectedPrice > 0 && (eYield != infos.Globals.ORDERS_YIELD))
            {
                int priceMultplier = -1 + game.getYieldBuyPrice(eYield) / expectedPrice; //how many times more the price is today than the regular price
                generalMultiplier += priceMultplier; 
            }

            return base.calculateYieldValue(eYield, iExtraStockpile, Math.Min(iExtraRate, - generalMultiplier * 300)); //treat an inflow of 30 goods * generalMultiplier as 0, so AI doesn't shoot for break even
        }

        protected override int calculateTargetMilitaryUnitNumber()
        {
            var target = base.calculateTargetMilitaryUnitNumber();
            DictionaryList<YieldType, int> shortages = getYieldShortages();
            if (shortages != null)
                target *= (6 - shortages.Count) / 5 ; // no shortage? get 6/5 x the military since you can afford it! a lot of shortage? fewer units because you are too bankrupt
            if (player != null) 
                target += player.countTeamWars() * 3 + countUnits(x => x?.getXP() < 50) / 2; //3 more unit per war; units with less than 50 xp count as half a unit
            return target;
        }
       
    }

}