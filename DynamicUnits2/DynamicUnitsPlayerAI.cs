using Mohawk.SystemCore;
using System;
using System.Reflection;
using System.Xml.Linq;
using TenCrowns.GameCore;
using UnityEngine;
using static TenCrowns.GameCore.Player;

namespace DynamicUnits
{
    internal class DynamicUnitsPlayerAI : Player.PlayerAI
    {


        protected int offset => player == null? 5: (player.getCapitalCityID() + player.getFounderID()) % 13  + 1; //1-13
        protected override int AI_MAX_WATER_CONTROL_DISTANCE => 15;
        protected override int AI_CULTURE_VALUE => 30 + offset; 
        protected override int AI_HAPPINESS_VALUE => base.AI_HAPPINESS_VALUE - 5 * offset;
        protected override int AI_ORDERS_VALUE =>  base.AI_ORDERS_VALUE + 20 * offset;
        protected override int AI_MONEY_VALUE => 7 + offset/2;
        protected override int AI_TRAINING_VALUE => base.AI_TRAINING_VALUE- offset;
        protected override int AI_GOODS_VALUE => AI_MONEY_VALUE * 4 + offset/3;
        protected override int AI_MONEY_STOCKPILE_TURNS => base.AI_MONEY_STOCKPILE_TURNS + offset;
        protected override int AI_NUM_GOODS_TARGET => base.AI_NUM_GOODS_TARGET + 20 * offset;

        protected override int AI_NO_WONDER_TURNS => base.AI_NO_WONDER_TURNS + offset;
        protected override int AI_WONDER_VALUE => base.AI_WONDER_VALUE - 50 * (13 - offset); //300 is the base; so this makes wonders' innate value around zero
        protected override int AI_VP_VALUE => base.AI_VP_VALUE - 30 * offset; //800
        protected override int AI_UNIT_SCOUT_VALUE => base.AI_UNIT_SCOUT_VALUE + offset;

        protected override int AI_UNIT_GENERAL_VALUE => base.AI_UNIT_GENERAL_VALUE + 20 * offset;

        protected override int AI_YIELD_TURNS => base.AI_YIELD_TURNS + offset;
        protected override int AI_UNIT_RANDOM_PROMOTION_VALUE => base.AI_UNIT_PROMOTE_VALUE/3*2;
            
        protected override int AI_TRADE_NETWORK_VALUE_ESTIMATE => base.AI_TRADE_NETWORK_VALUE_ESTIMATE + 100 * offset;
        protected override int AI_BUILD_URBAN_VALUE => base.AI_BUILD_URBAN_VALUE + 3 * offset;
        //  protected override int AI_IDLE_XP_VALUE /= 2;
        //  protected override int AI_CITY_REBEL_VALUE /= 2;
        protected override int AI_WASTED_EFFECT_VALUE => base.AI_WASTED_EFFECT_VALUE - offset;
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
        protected override long getRoadValue(City pCity, bool bMovement, bool bConnection, bool bRemove, City pOtherCity)
        {
            //road connections are way more valuable than vanilla
            return base.getRoadValue(pCity, bMovement, bConnection, bRemove, pOtherCity) * (bConnection? 2: 5);
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