﻿using System;
using TenCrowns.GameCore;
using UnityEngine;

namespace DynamicUnits
{
    internal class DynamicUnitsPlayerAI : Player.PlayerAI
    {
        
        public override void init(Game pGame, Player pPlayer, Tribe pTribe)
        {
            updateAIPriorities(pGame.randomNext(13));
            base.init(pGame,pPlayer, pTribe);
        }
        private void updateAIPriorities(int seed)
        {
            int offset = 1 + (seed % 13);  
            AI_MAX_WATER_CONTROL_DISTANCE = 15;
            AI_GROWTH_VALUE -= offset;
            AI_CULTURE_VALUE = 30 + offset;
            AI_HAPPINESS_VALUE -= 5 * offset;
            AI_ORDERS_VALUE /= 2;
            AI_MONEY_VALUE = 7 + offset;
            AI_TRAINING_VALUE -= offset;
            AI_GOODS_VALUE = AI_MONEY_VALUE * 5 - offset;
            AI_MONEY_STOCKPILE_TURNS /= 2;
            AI_NUM_GOODS_TARGET = 200 * offset;
            AI_TURNS_BETWEEN_KILLS += offset;
            AI_CHARACTER_OPINION_VALUE *= 2;
            AI_NO_WONDER_TURNS = 5 + offset * 2;
            AI_WONDER_VALUE -= 50 * (13 - offset);
            AI_VP_VALUE /= 2;
            AI_UNIT_LEVEL_VALUE *= 2;
            AI_UNIT_PUSH_VALUE *= 2;
            AI_UNIT_ROUT_VALUE *= 2;
            AI_ENLIST_ON_KILL_VALUE *= 3;
            AI_UNIT_LAST_STAND_VALUE *= 2;
            AI_UNIT_FORTIFY_VALUE = 100;
            AI_UNIT_PROMOTE_VALUE *= 2;
            AI_UNIT_GENERAL_VALUE *= 2;

            AI_YIELD_TURNS += 3 * offset;
           // AI_YIELD_SHORTAGE_PER_TURN_MODIFIER /= 2;
            AI_UNIT_RANDOM_PROMOTION_VALUE = AI_UNIT_PROMOTE_VALUE/2;
            
            AI_TRADE_NETWORK_VALUE_ESTIMATE = 400 + 40*offset;
            AI_BUILD_URBAN_VALUE *= 4;
            AI_IDLE_XP_VALUE /= 2;
            AI_CITY_REBEL_VALUE /= 3;
            AI_MAX_NUM_WORKERS_PER_HUNDRED_CITIES = 200;
            AI_WASTED_EFFECT_VALUE = -offset;
            AI_MAX_FORT_BORDER_DISTANCE_INSIDE = 2;

        }
       
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
            //inmobile units worth less
            long val = base.calculateUnitValue(eUnit);
            if (isSettler(eUnit))
            {
                val *= 2;
                val /= 1+ countUnits(IsSettlerDelegate);
                
            }
            return infos.unit(eUnit).miMovement < 1 ? val / 8: val;
        }

    public override int getWarOfferPercent(PlayerType eOtherPlayer, bool bDeclare)
        {
            
            int chance = base.getWarOfferPercent(eOtherPlayer, bDeclare);
            int desire = ((DynamicUnitsPlayer)(player)).desirePeace(eOtherPlayer);
            if (desire < 0)
                chance -= desire / 20; //desire is between -200 and 0; so this increases chance by up to 10% 
           
            chance += player.getOrdersLeft() / 10 - player.countTeamWars() * 3; //for every 30 orders, AI wants to be in 1 war, at a rate of 1% of 10 order of exccess 
            chance = infos.utils().range(chance, 0, 30);
            return chance;
        }
    }
}