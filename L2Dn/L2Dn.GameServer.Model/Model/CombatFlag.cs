﻿using System.Runtime.CompilerServices;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.Geometry;

namespace L2Dn.GameServer.Model;

public class CombatFlag
{
	private Player? _player;
	private int _playerId;
	private Item? _item;
	private Item? _itemInstance;
	private readonly LocationHeading _location;
	private readonly int _itemId;
	private readonly int _fortId;
	
	public CombatFlag(int fortId, int x, int y, int z, int heading, int itemId)
	{
		_fortId = fortId;
		_location = new LocationHeading(x, y, z, heading);
		_itemId = itemId;
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	public void spawnMe()
	{
		// Init the dropped ItemInstance and add it in the world as a visible object at the position where mob was last
		_itemInstance = ItemData.getInstance().createItem("Combat", _itemId, 1, null, null);
		_itemInstance.dropMe(null, _location.X, _location.Y, _location.Z);
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	public void unSpawnMe()
	{
		if (_player != null)
		{
			dropIt();
		}
		if (_itemInstance != null)
		{
			_itemInstance.decayMe();
		}
	}
	
	public bool activate(Player player, Item item)
	{
		if (player.isMounted())
		{
			player.sendPacket(SystemMessageId.YOU_DO_NOT_MEET_THE_REQUIRED_CONDITION_TO_EQUIP_THAT_ITEM);
			return false;
		}
		
		// Player holding it data
		_player = player;
		_playerId = _player.getObjectId();
		_itemInstance = null;
		
		// Equip with the weapon
		_item = item;
		_player.getInventory().equipItem(_item);
		SystemMessagePacket sm = new SystemMessagePacket(SystemMessageId.S1_EQUIPPED);
		sm.Params.addItemName(_item);
		_player.sendPacket(sm);
		
		// Refresh inventory
		InventoryUpdatePacket iu = new InventoryUpdatePacket(new ItemInfo(_item));
		_player.sendInventoryUpdate(iu);
		
		// Refresh player stats
		_player.broadcastUserInfo();
		_player.setCombatFlagEquipped(true);
		return true;
	}
	
	public void dropIt()
	{
		// Reset player stats
		_player.setCombatFlagEquipped(false);
		long slot = _player.getInventory().getSlotFromItem(_item);
		_player.getInventory().unEquipItemInBodySlot(slot);
		_player.destroyItem("CombatFlag", _item, null, true);
		_item = null;
		_player.broadcastUserInfo();
		_player = null;
		_playerId = 0;
	}
	
	public int getPlayerObjectId()
	{
		return _playerId;
	}
	
	public Item getCombatFlagInstance()
	{
		return _itemInstance;
	}
}
