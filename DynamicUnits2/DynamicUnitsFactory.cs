using TenCrowns.ClientCore;
using TenCrowns.GameCore;

namespace DynamicUnits
{
    internal class DynamicUnitsFactory : GameFactory
    {
        public override Unit CreateUnit()
        {
            return new DUUnits();
        }
        public override InfoHelpers CreateInfoHelpers(Infos pInfos)
        {
            return new DynamicUnitsInfoHelper(pInfos);
        }
        public override Player CreatePlayer()
        {
            return new DynamicUnitsPlayer();
        }
        public override Player.PlayerAI CreatePlayerAI()
        {
            return new DynamicUnitsPlayerAI();
        }
        public override Tile CreateTile()
        {
            return new DUTiles();
        }
        public override HelpText CreateHelpText(ModSettings pModSettings)
        {
            return new DUHelpText(pModSettings);
        }
        public override Game CreateGame(ModSettings pModSettings, IApplication pApp, bool bShowGame)
        {
            return new DUGame(pModSettings, pApp, bShowGame);
        }


    }
}