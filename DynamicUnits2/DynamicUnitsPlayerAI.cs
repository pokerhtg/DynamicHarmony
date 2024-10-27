using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using TenCrowns.GameCore;
using UnityEngine;
using static TenCrowns.GameCore.Player;

namespace DynamicUnits
{
    internal class DynamicUnitsPlayerAI : Player.PlayerAI
    {
        private int maxDistanceALlowed = 10;

        protected int offset => player == null? 5: (player.getCapitalCityID() *2 + 3* player.getFounderID()) % 7  - 2; //-2 to 5
        protected override int AI_MAX_WATER_CONTROL_DISTANCE => 15;
        protected override int AI_CULTURE_VALUE => base.AI_CULTURE_VALUE + offset; 
        protected override int AI_HAPPINESS_VALUE => base.AI_HAPPINESS_VALUE - 5 * offset;
        protected override int AI_ORDERS_VALUE =>  base.AI_ORDERS_VALUE + 20 * offset;
        protected override int AI_MONEY_VALUE => 5 + offset/2;
        protected override int AI_TRAINING_VALUE => base.AI_TRAINING_VALUE- offset;
        protected override int AI_GOODS_VALUE => AI_MONEY_VALUE * 4;
        protected override int AI_MONEY_STOCKPILE_TURNS => base.AI_MONEY_STOCKPILE_TURNS + offset;
        protected override int AI_NUM_GOODS_TARGET => base.AI_NUM_GOODS_TARGET + 20 * offset;

        protected override int AI_NO_WONDER_TURNS => base.AI_NO_WONDER_TURNS + offset;
       
        protected override int AI_VP_VALUE => base.AI_VP_VALUE - 30 * offset; //800
        protected override int AI_UNIT_SCOUT_VALUE => base.AI_UNIT_SCOUT_VALUE + offset;

        protected override int AI_UNIT_GENERAL_VALUE => base.AI_UNIT_GENERAL_VALUE + 20 * offset;

        protected override int AI_YIELD_TURNS => base.AI_YIELD_TURNS + offset;
        protected override int AI_UNIT_RANDOM_PROMOTION_VALUE => base.AI_UNIT_PROMOTE_VALUE/3*2;
            
        protected override int AI_TRADE_NETWORK_VALUE_ESTIMATE => base.AI_TRADE_NETWORK_VALUE_ESTIMATE * (26 + offset) / 26;
        protected override int AI_BUILD_URBAN_VALUE => base.AI_BUILD_URBAN_VALUE + 2 * offset;
        //  protected override int AI_IDLE_XP_VALUE /= 2;
        //  protected override int AI_CITY_REBEL_VALUE /= 2;
        protected override int AI_MAX_FORT_BORDER_DISTANCE_INSIDE => base.AI_MAX_FORT_BORDER_DISTANCE_INSIDE + offset/2;
        protected override int AI_MAX_FORT_RIVAL_BORDER_DISTANCE => base.AI_MAX_FORT_RIVAL_BORDER_DISTANCE + (12-offset);
        protected override int AI_MAX_FORT_BORDER_DISTANCE_OUTSIDE => AI_MAX_FORT_BORDER_DISTANCE_INSIDE/2;
        

        protected override bool isFoundCitySafe(Tile pTile)
        {
            if (getDistanceFromNationBorder(pTile) < infos.Globals.MIN_CITY_SITE_DISTANCE)
                return true;
            else return base.isFoundCitySafe(pTile);
        }
        public override long getFortValue(ImprovementType eImprovement, Tile pTile)
        {
            //defense structures aren't that important
            return base.getFortValue(eImprovement, pTile)/2;  
        }
       
        protected override long calculateUnitValue(UnitType eUnit)
        {
            long val = base.calculateUnitValue(eUnit);
            //inmobile units worth less
            if (infos.unit(eUnit).miMovement < 1)
                val /= 8;
            //navy isn't as important as their land counterparts
            if (isWarship(eUnit))
                val /= 2; 
                return val;
        }

        protected override long citizenValue(City pCity, bool bRemove)
        {
            return base.citizenValue(pCity, bRemove) / 2;//DU and DW both makes citizen more of a problem; AI should value them less.
        }
        protected override void modifyImprovementValue(ImprovementType eImprovement, Tile pTile, City pCity, ref long iValue)
        {
            base.modifyImprovementValue(eImprovement,pTile, pCity, ref iValue);
            if (pTile.isSettlement() && eImprovement != pTile.getImprovement()){
                iValue += 10 * AI_EXISTING_IMPROVEMENT_VALUE; //encourage building over existing tribal structures
            }
        }

        public override void updateCityDanger()
        { 
            base.updateCityDanger();
            //     if (player != null && player.capitalCity()!= null)
            //       Debug.Log(player.capitalCity().getPlayerInt() + " 's offset is " + offset);
            if (player == null)
                return;
            foreach (int iLoopCity in player.getCities())
            {
                var rebelChance = game.city(iLoopCity).calculateRebelProb();
                if (rebelChance > 0)
                {
                    mpAICache.setCityDanger(iLoopCity, 20 * rebelChance); //100 is the danger presented by an avg-low unit; so 20 is 1 fifth that. every 5% rebellion chance is treated like one weak rebel unit
                }
            }
        }

        //AI spreads itself too thin. Let's limit its preceived area of control
        public override bool isTileReachable(Tile pToTile, Unit pUnit)
        {
            bool result = base.isTileReachable(pToTile, pUnit);
            if (result)
                if (!pUnit.isScout() && pUnit.hasPlayer() && pUnit.player().capitalCity() != null)
                {  //not a scout, has a capital
                    var dstanceAllowed = pUnit.movement() * pUnit.getFatigueLimit() * (5 * player.countMilitaryUnits() + game.getTurn()) / 25 / (pUnit.AI.isBelowHealthPercent(80) ? 3 : 2);//and want to venture too far from your own land? 
                    if (dstanceAllowed > maxDistanceALlowed)
                        maxDistanceALlowed = dstanceAllowed;
                    if (pUnit.movement() < 1 || pUnit.player().AI.getClosestCityDistance(pToTile) > maxDistanceALlowed)       //once a distance is allowed, don't ever unallow it, unless territory shrinks, then oh well.                                    
                        return false;
                }
            return result;
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
            /**
            // public virtual int getWarOfferPercent(PlayerType eOtherPlayer, bool bDeclare = true, bool bPreparedOnly = false, bool bCurrentPlayer = true
            public override int getWarOfferPercent(PlayerType eOtherPlayer, bool bDeclare, bool bPreparedOnly = false, bool bCurrentPlayer = true)
            {
                int chance = base.getWarOfferPercent(eOtherPlayer, bDeclare, bPreparedOnly, bCurrentPlayer);

            chance -= getDistanceFromNationBorder(game.player(eOtherPlayer).capitalCity().tile()) / 5;
                if (!bPreparedOnly)
                {
                    int desire = ((DynamicUnitsPlayer)(player)).desirePeace(eOtherPlayer);
                    if (desire< -50)
                        chance -= desire / 20; //desire is between -200 and 0; so this increases chance by up to 10% 

                    chance += player.getOrdersLeft() / 10 - player.countTeamWars()* 4; //for every 40 orders, AI wants to be in 1 war, at a rate of 1% of 10 order of exccess 
                }
        chance = infos.utils().range(chance, 0, 35);
                return chance;
            }
            **/
        }

}