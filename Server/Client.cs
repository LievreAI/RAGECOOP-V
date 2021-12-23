﻿using System;
using System.Collections.Generic;

using Lidgren.Network;

namespace CoopServer
{
    public class Client
    {
        public long NetHandle = 0;
        public float Latency = 0.0f;
        public PlayerData Player;
        private readonly Dictionary<string, object> CustomData = new();
        internal readonly Dictionary<long, Action<object>> Callbacks = new();

        #region CUSTOMDATA FUNCTIONS
        public void SetData<T>(string name, T data)
        {
            if (HasData(name))
            {
                CustomData[name] = data;
            }
            else
            {
                CustomData.Add(name, data);
            }
        }

        public bool HasData(string name)
        {
            return CustomData.ContainsKey(name);
        }

        public T GetData<T>(string name)
        {
            return HasData(name) ? (T)CustomData[name] : default;
        }

        public void RemoveData(string name)
        {
            if (HasData(name))
            {
                CustomData.Remove(name);
            }
        }
        #endregion

        #region FUNCTIONS
        public void Kick(string[] reason)
        {
            Server.MainNetServer.Connections.Find(x => x.RemoteUniqueIdentifier == NetHandle)?.Disconnect(string.Join(" ", reason));
        }
        public void Kick(string reason)
        {
            Server.MainNetServer.Connections.Find(x => x.RemoteUniqueIdentifier == NetHandle)?.Disconnect(reason);
        }

        public void SendChatMessage(string message, string from = "Server")
        {
            try
            {
                NetConnection userConnection = Server.MainNetServer.Connections.Find(x => x.RemoteUniqueIdentifier == NetHandle);
                if (userConnection == null)
                {
                    return;
                }

                Server.SendChatMessage(from, message, userConnection);
            }
            catch (Exception e)
            {
                Logging.Error($">> {e.Message} <<>> {e.Source ?? string.Empty} <<>> {e.StackTrace ?? string.Empty} <<");
            }
        }

        public void SendNativeCall(ulong hash, List<object> args = null)
        {
            try
            {
                NetConnection userConnection = Server.MainNetServer.Connections.Find(x => x.RemoteUniqueIdentifier == NetHandle);
                if (userConnection == null)
                {
                    Logging.Error($"[Client->SendNativeCall(ulong hash, params object[] args)]: Connection \"{NetHandle}\" not found!");
                    return;
                }

                if (args != null && args.Count == 0)
                {
                    Logging.Error($"[Client->SendNativeCall(ulong hash, Dictionary<string, object> args)]: Missing arguments!");
                    return;
                }

                NativeCallPacket packet = new()
                {
                    Hash = hash,
                    Args = args ?? new List<object>(),
                };

                NetOutgoingMessage outgoingMessage = Server.MainNetServer.CreateMessage();
                packet.PacketToNetOutGoingMessage(outgoingMessage);
                Server.MainNetServer.SendMessage(outgoingMessage, userConnection, NetDeliveryMethod.ReliableOrdered, (byte)ConnectionChannel.Native);
            }
            catch (Exception e)
            {
                Logging.Error($">> {e.Message} <<>> {e.Source ?? string.Empty} <<>> {e.StackTrace ?? string.Empty} <<");
            }
        }

        public void SendNativeResponse(Action<object> callback, ulong hash, Type returnType, List<object> args = null)
        {
            try
            {
                NetConnection userConnection = Server.MainNetServer.Connections.Find(x => x.RemoteUniqueIdentifier == NetHandle);
                if (userConnection == null)
                {
                    Logging.Error($"[Client->SendNativeResponse(Action<object> callback, ulong hash, Type type, params object[] args)]: Connection \"{NetHandle}\" not found!");
                    return;
                }

                if (args != null && args.Count == 0)
                {
                    Logging.Error($"[Client->SendNativeCall(ulong hash, Dictionary<string, object> args)]: Missing arguments!");
                    return;
                }

                long id = 0;
                Callbacks.Add(id = Environment.TickCount64, callback);

                byte returnTypeValue = 0x00;
                if (returnType == typeof(int))
                {
                    // NOTHING BECAUSE VALUE IS 0x00
                }
                else if (returnType == typeof(bool))
                {
                    returnTypeValue = 0x01;
                }
                else if (returnType == typeof(float))
                {
                    returnTypeValue = 0x02;
                }
                else if (returnType == typeof(string))
                {
                    returnTypeValue = 0x03;
                }
                else if (returnType == typeof(LVector3))
                {
                    returnTypeValue = 0x04;
                }
                else
                {
                    Logging.Error($"[Client->SendNativeCall(ulong hash, Dictionary<string, object> args)]: Missing return type!");
                    return;
                }

                NetOutgoingMessage outgoingMessage = Server.MainNetServer.CreateMessage();
                new NativeResponsePacket()
                {
                    Hash = hash,
                    Args = args ?? new List<object>(),
                    ResultType = returnTypeValue,
                    ID = id
                }.PacketToNetOutGoingMessage(outgoingMessage);
                Server.MainNetServer.SendMessage(outgoingMessage, userConnection, NetDeliveryMethod.ReliableOrdered, (byte)ConnectionChannel.Native);
            }
            catch (Exception e)
            {
                Logging.Error($">> {e.Message} <<>> {e.Source ?? string.Empty} <<>> {e.StackTrace ?? string.Empty} <<");
            }
        }

        public void SendModPacket(string mod, byte customID, byte[] bytes)
        {
            try
            {
                NetConnection userConnection = Server.MainNetServer.Connections.Find(x => x.RemoteUniqueIdentifier == NetHandle);
                if (userConnection == null)
                {
                    return;
                }

                NetOutgoingMessage outgoingMessage = Server.MainNetServer.CreateMessage();
                new ModPacket()
                {
                    NetHandle = 0,
                    Target = 0,
                    Mod = mod,
                    CustomPacketID = customID,
                    Bytes = bytes
                }.PacketToNetOutGoingMessage(outgoingMessage);
                Server.MainNetServer.SendMessage(outgoingMessage, userConnection, NetDeliveryMethod.ReliableOrdered, (byte)ConnectionChannel.Mod);
                Server.MainNetServer.FlushSendQueue();
            }
            catch (Exception e)
            {
                Logging.Error($">> {e.Message} <<>> {e.Source ?? string.Empty} <<>> {e.StackTrace ?? string.Empty} <<");
            }
        }
        #endregion
    }
}
