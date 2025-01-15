
using TenCrowns.AppCore;
using TenCrowns.GameCore;
using System;
using System.Diagnostics;
using static TenCrowns.GameCore.Unit;
namespace DynamicUnits
{
	internal class DUUnitAI : Unit.UnitAI
	{

		//just to wrap and expose the private method
		public void AttackFromCurrentTile(bool bKillOnly)
		{ 
			base.doAttackFromCurrentTile(bKillOnly);
		}

    }
}