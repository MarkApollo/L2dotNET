﻿using System.Linq;
using L2dotNET.model.npcs;
using L2dotNET.model.player;
using L2dotNET.Network.serverpackets;
using L2dotNET.tables;

namespace L2dotNET.Network.clientpackets
{
    class RequestBuyItem : PacketBase
    {
        private readonly GameClient _client;
        private readonly int _listId;
        private readonly int _count;
        private readonly int[] _items;

        public RequestBuyItem(Packet packet, GameClient client)
        {
            _client = client;
            _listId = packet.ReadInt();
            _count = packet.ReadInt();

            _items = new int[_count * 2];
            for (int i = 0; i < _count; i++)
            {
                _items[i * 2] = packet.ReadInt();
                _items[(i * 2) + 1] = packet.ReadInt();
            }
        }

        public override void RunImpl()
        {
            L2Player player = _client.CurrentPlayer;

            if (_count <= 0)
            {
                player.SendActionFailed();
                return;
            }

            L2Npc trader = player.FolkNpc;

            if (trader == null)
            {
                player.SendSystemMessage(SystemMessage.SystemMessageId.TradeAttemptFailed);
                player.SendActionFailed();
                return;
            }

            NDShop shop = NpcData.Instance.Shops[trader.Template.NpcId];

            if (shop == null)
            {
                player.SendSystemMessage(SystemMessage.SystemMessageId.TradeAttemptFailed);
                player.SendActionFailed();
                return;
            }

            NdShopList list = shop.Lists[(short)_listId];

            if (list == null)
            {
                player.SendSystemMessage(SystemMessage.SystemMessageId.TradeAttemptFailed);
                player.SendActionFailed();
                return;
            }

            int adena = 0;
            int slots = 0,
                weight = 0;

            for (int i = 0; i < _count; i++)
            {
                int itemId = _items[i * 2];

                bool notfound = true;
                foreach (NDShopItem item in list.Items.Where(item => item.Item.ItemId == itemId))
                {
                    adena += item.Item.ReferencePrice * _items[(i * 2) + 1];

                    if (!item.Item.Stackable)
                        slots++;
                    //else
                    //{
                    //    if (!player.HasItem(item.item.ItemID))
                    //        slots++;
                    //}

                    weight += item.Item.Weight * _items[(i * 2) + 1];

                    notfound = false;
                    break;
                }

                if (!notfound)
                    continue;

                player.SendSystemMessage(SystemMessage.SystemMessageId.TradeAttemptFailed);
                player.SendActionFailed();
                return;
            }

            if (adena > player.GetAdena())
            {
                player.SendSystemMessage(SystemMessage.SystemMessageId.YouNotEnoughAdena);
                return;
            }

            player.ReduceAdena(adena);

            for (int i = 0; i < _count; i++)
            {
                int itemId = _items[i * 2];
                int count = _items[(i * 2) + 1];

                player.AddItem(itemId, count);
            }

            player.SendPacket(new ExBuySellListClose());
        }
    }
}