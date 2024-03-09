using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;

namespace L2Dn.GameServer.Handlers.ActionShiftHandlers;

public class PlayerActionShift: IActionShiftHandler
{
	public bool action(Player player, WorldObject target, bool interact)
	{
		if (player.isGM())
		{
			// Check if the GM already target this l2pcinstance
			if (player.getTarget() != target)
			{
				// Set the target of the Player player
				player.setTarget(target);
			}
			
			AdminCommandHandler.getInstance().useAdminCommand(player, "admin_character_info " + target.getName(), true);
		}
		return true;
	}
	
	public InstanceType getInstanceType()
	{
		return InstanceType.Player;
	}
}