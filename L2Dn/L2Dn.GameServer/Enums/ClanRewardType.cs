﻿namespace L2Dn.GameServer.Enums;

public enum ClanRewardType
{
    MEMBERS_ONLINE,
    HUNTING_MONSTERS
}
//
// public enum ClanRewardType
// {
//     MEMBERS_ONLINE(0, Clan::getPreviousMaxOnlinePlayers),
//     HUNTING_MONSTERS(1, Clan::getPreviousHuntingPoints);
// 	
//     final int _clientId;
//     final int _mask;
//     final Function<Clan, Integer> _pointsFunction;
// 	
//     ClanRewardType(int clientId, Function<Clan, Integer> pointsFunction)
//     {
//     _clientId = clientId;
//     _mask = 1 << clientId;
//     _pointsFunction = pointsFunction;
// }
// 	
// public int getClientId()
// {
//     return _clientId;
// }
// 	
// public int getMask()
// {
//     return _mask;
// }
// 	
// public ClanRewardBonus getAvailableBonus(Clan clan)
// {
//     ClanRewardBonus availableBonus = null;
//     for (ClanRewardBonus bonus : ClanRewardData.getInstance().getClanRewardBonuses(this))
//     {
//         if (bonus.getRequiredAmount() <= _pointsFunction.apply(clan))
//         {
//             if ((availableBonus == null) || (availableBonus.getLevel() < bonus.getLevel()))
//             {
//                 availableBonus = bonus;
//             }
//         }
//     }
//     return availableBonus;
// }
// 	
// public static int getDefaultMask()
// {
//     int mask = 0;
//     for (ClanRewardType type : values())
//     {
//         mask |= type.getMask();
//     }
//     return mask;
// }
// }
