using System.Text;
using L2Dn.GameServer.Data;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Clans;
using L2Dn.GameServer.Model.Sieges;
using L2Dn.GameServer.Model.Zones.Types;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;

namespace L2Dn.GameServer.Handlers.UserCommandHandlers;

/**
 * @author Tryskell
 */
public class SiegeStatus: IUserCommandHandler
{
	private static readonly int[] COMMAND_IDS =
	{
		99
	};
	
	private static readonly String INSIDE_SIEGE_ZONE = "Castle Siege in Progress";
	private static readonly String OUTSIDE_SIEGE_ZONE = "No Castle Siege Area";
	
	public bool useUserCommand(int id, Player player)
	{
		if (id != COMMAND_IDS[0])
		{
			return false;
		}
		
		if (!player.isNoble() || !player.isClanLeader())
		{
			player.sendPacket(SystemMessageId.ONLY_A_CLAN_LEADER_THAT_IS_A_NOBLESSE_OR_EXALTED_CAN_VIEW_THE_SIEGE_STATUS_WINDOW_DURING_A_SIEGE_WAR);
			return false;
		}
		
		foreach (Siege siege in SiegeManager.getInstance().getSieges())
		{
			if (!siege.isInProgress())
			{
				continue;
			}
			
			Clan clan = player.getClan();
			if (!siege.checkIsAttacker(clan) && !siege.checkIsDefender(clan))
			{
				continue;
			}
			
			SiegeZone siegeZone = siege.getCastle().getZone();
			StringBuilder sb = new StringBuilder();
			foreach (Player member in clan.getOnlineMembers(0))
			{
				sb.Append("<tr><td width=170>");
				sb.Append(member.getName());
				sb.Append("</td><td width=100>");
				sb.Append(siegeZone.isInsideZone(member) ? INSIDE_SIEGE_ZONE : OUTSIDE_SIEGE_ZONE);
				sb.Append("</td></tr>");
			}

			HtmlPacketHelper helper = new HtmlPacketHelper(DataFileLocation.Data, "html/siege/siege_status.htm");
			helper.Replace("%kill_count%", clan.getSiegeKills().ToString());
			helper.Replace("%death_count%", clan.getSiegeDeaths().ToString());
			helper.Replace("%member_list%", sb.ToString());
			NpcHtmlMessagePacket html = new NpcHtmlMessagePacket(helper);
			player.sendPacket(html);
			
			return true;
		}
		
		player.sendPacket(SystemMessageId.ONLY_A_CLAN_LEADER_THAT_IS_A_NOBLESSE_OR_EXALTED_CAN_VIEW_THE_SIEGE_STATUS_WINDOW_DURING_A_SIEGE_WAR);
		
		return false;
	}
	
	public int[] getUserCommandList()
	{
		return COMMAND_IDS;
	}
}