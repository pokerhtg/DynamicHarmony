using TenCrowns.GameCore;
using TenCrowns.GameCore.Text;

namespace DynamicUnits
{
    internal class DUHelpText : HelpText
    {
        public DUHelpText(ModSettings modsettings) : base(modsettings)
        {
        }
        public override TextBuilder buildUnitTypeHelp(TextBuilder builder, UnitType eUnit, City pCity, Player pPlayer, TribeType eTribe, Game pGame, Player pActivePlayer, bool bName = true, bool bCosts = true, bool bStats = true, bool bDetails = true)
        {
            var result = base.buildUnitTypeHelp(builder, eUnit, pCity, pPlayer, eTribe, pGame, pActivePlayer, bName, bCosts, bStats, bDetails);
            var Helper = (DynamicUnitsInfoHelper)infos().Helpers;
            if (Helper.isMountaineer(eUnit))
            {
              builder.AddTEXT("TEXT_HELPTEXT_MOUNTAIN_TRAVERSAL");
            }
            return result;
        }
    }
}