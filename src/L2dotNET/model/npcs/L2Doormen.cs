﻿using System.Linq;
using L2dotNET.model.npcs.decor;
using L2dotNET.model.player;
using L2dotNET.model.structures;
using L2dotNET.Network.serverpackets;

namespace L2dotNET.model.npcs
{
    class L2Doormen : L2Npc
    {
        private readonly Hideout _hideout;

        public L2Doormen(HideoutTemplate hideout)
        {
            _hideout = (Hideout)hideout;
            StructureControlled = true;
        }

        public override void NotifyAction(L2Player player)
        {
            if (_hideout.ownerId != player.ClanId)
                return;

            NpcHtmlMessage htm = new NpcHtmlMessage(player, "agitjanitorhi.htm", ObjId);
            htm.Replace("<?my_pledge_name?>", player.Clan.Name);
            player.SendPacket(htm);
        }

        public override void OnDialog(L2Player player, int ask, int reply)
        {
            player.FolkNpc = this;

            switch (ask)
            {
                case 0:
                    NotifyAction(player);
                    break;
                case -203:
                    switch (reply)
                    {
                        case 1: //open ch doors
                            foreach (L2Door door in _hideout.doors.Where(door => door.Closed != 0))
                            {
                                door.Closed = 0;
                                door.BroadcastUserInfo();
                            }

                            player.SendPacket(new NpcHtmlMessage(player, "AgitJanitorAfterDoorOpen.htm", ObjId));
                            break;
                        case 2: //close
                            foreach (L2Door door in _hideout.doors.Where(door => door.Closed != 1))
                            {
                                door.Closed = 1;
                                door.BroadcastUserInfo();
                            }

                            player.SendPacket(new NpcHtmlMessage(player, "AgitJanitorAfterDoorClose.htm", ObjId));
                            break;
                    }

                    break;
            }
        }

        public override string AsString()
        {
            return $"L2Doormen:{Template.NpcId}; id {ObjId}; {_hideout.ID}";
        }
    }
}