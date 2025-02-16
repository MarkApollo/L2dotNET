﻿using System;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Contexts;
using log4net;
using L2dotNET.Network.loginauth;
using L2dotNET.Network.loginauth.recv;

namespace L2dotNET.Network
{
    [Synchronization]
    public class GamePacketHandlerAuth
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GamePacketHandlerAuth));

        private static readonly ConcurrentDictionary<byte, Type> ClientPackets = new ConcurrentDictionary<byte, Type>();
        private static readonly ConcurrentDictionary<byte, Type> ClientPacketsSerc = new ConcurrentDictionary<byte, Type>();

        static GamePacketHandlerAuth()
        {
            ClientPackets.TryAdd(0xA1, typeof(LoginServPingResponse));
            ClientPackets.TryAdd(0xA5, typeof(LoginServLoginFail));
            ClientPackets.TryAdd(0xA6, typeof(LoginServLoginOk));
            ClientPackets.TryAdd(0xA7, typeof(LoginServAcceptPlayer));
            ClientPackets.TryAdd(0xA8, typeof(LoginServKickAccount));
        }

        public static void HandlePacket(Packet packet, AuthThread login)
        {
            PacketBase packetBase = null;
            Log.Info($"Received packet with Opcode:{packet.FirstOpcode.ToString("X2")}");
            if (ClientPackets.ContainsKey(packet.FirstOpcode))
                packetBase = (PacketBase)Activator.CreateInstance(ClientPackets[packet.FirstOpcode], packet, login);
            packetBase?.RunImpl();
        }
    }
}