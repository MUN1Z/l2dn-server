﻿using L2Dn.Events;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Quests;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Model.Events.Impl.Npcs;

public class OnNpcTalk: EventBase
{
    private readonly Npc _npc;
    private readonly Player _player;
	
    public OnNpcTalk(Npc npc, Player player)
    {
        _npc = npc;
        _player = player;
    }
	
    public Npc getNpc()
    {
        return _npc;
    }
	
    public Player getActiveChar()
    {
        return _player;
    }

    public Set<Quest> Quests { get; } = new();
}