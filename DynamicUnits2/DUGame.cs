using System;
using TenCrowns.ClientCore;
using TenCrowns.GameCore;

namespace DynamicUnits
{
    internal class DUGame : Game
    {
        public DUGame(ModSettings pModSettings, IApplication pApp, bool bShowGame) : base(pModSettings, pApp, bShowGame)
        {
           
        }
        protected override void postStart()
        {
           foreach (InfoHeight h in infos().heights())
            {
                if (h.mbImpassable && h.miMovementCost > 1)
                {
                    //if you have a movement cost, you aren't actually inpassable
                    h.mbImpassable = false; 
                }
            }
           base.postStart();
        }
        public override bool initFromMapScript(GameParameters pGameParams, MapBuilder pMapBuilder)
        {
            foreach (InfoHeight h in infos().heights())
            {
                if (!h.mbImpassable && h.miMovementCost > 15)
                {
                    //if your movement cost is high, treat it like inpassable for map gen
                    h.mbImpassable = true;
                }
            }
            return base.initFromMapScript(pGameParams, pMapBuilder);
        }

        public override int getTeamWarScore(TeamType eIndex1, TeamType eIndex2)
        {
            
            int perception = ((DynamicUnitsPlayer)firstTeamPlayer(eIndex1)).desirePeace(firstTeamPlayer(eIndex2).getPlayer());
            var rawWarScore = mpCurrentData.maaiTeamWarScore[(int)eIndex1, (int)eIndex2];
            int perceiptionWeight = 3;
            //20 year war gives standard desire for peace. shorter wars aren't as conclusive so desire is closer to 0
            int conclusivePercent = (60 + 2 *getTeamConflictNumTurns(eIndex1, eIndex2));
          

            return (perceiptionWeight * perception + rawWarScore) * conclusivePercent / 100;
        }
        protected override int getSellPrice(int iPrice, Player pPlayer)
        {
            int salePrice = Math.Max(1, getBuyPrice(iPrice) * infos().Globals.PRICE_SELL_PERCENT / 100);
            if (pPlayer?.isNoSellPenaltyUnlock() ?? false)
            {
                return salePrice * 2;
            }
            else
            {
                return salePrice;
            }
        }
    }
}