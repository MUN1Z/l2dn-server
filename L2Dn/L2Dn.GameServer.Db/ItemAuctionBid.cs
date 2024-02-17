﻿using Microsoft.EntityFrameworkCore;

namespace L2Dn.GameServer.Db;

[PrimaryKey(nameof(AuctionId), nameof(CharacterId))]
public class ItemAuctionBid
{
    public int AuctionId { get; set; }
    public int CharacterId { get; set; }
    public long Bid { get; set; }
}