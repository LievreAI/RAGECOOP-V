﻿

namespace RageCoop.Client
{
    /// <summary>
    ///     Don't use it!
    /// </summary>
    public class ClientSettings
    {
        /// <summary>
        ///     LogLevel for RageCoop.
        ///     0:Trace, 1:Debug, 2:Info, 3:Warning, 4:Error
        /// </summary>
        public int LogLevel = 1;

        /// <summary>
        ///     Don't use it!
        /// </summary>
        public string Username { get; set; } = "Player";

        /// <summary>
        ///     The password used to authenticate when connecting to a server.
        /// </summary>
        public string Password { get; set; } = "";

        /// <summary>
        ///     Don't use it!
        /// </summary>
        public string LastServerAddress { get; set; } = "127.0.0.1:4499";

        /// <summary>
        ///     Don't use it!
        /// </summary>
        public string MasterServer { get; set; } = "https://test.ragecoop.com/";

        /// <summary>
        ///     Don't use it!
        /// </summary>
        public bool FlipMenu { get; set; } = false;

        /// <summary>
        ///     Don't use it!
        /// </summary>
        public bool Voice { get; set; } = false;

        /// <summary>
        ///     The key to open menu
        /// </summary>
        public Keys MenuKey { get; set; } = Keys.F7;

        /// <summary>
        ///     The key to enter a vehicle as passenger.
        /// </summary>
        public Keys PassengerKey { get; set; } = Keys.G;

        /// <summary>
        ///     Disable world NPC traffic, mission entities won't be affected
        /// </summary>
        public bool DisableTraffic { get; set; } = false;

        /// <summary>
        ///     Bring up pause menu but don't freeze time when FrontEndPauseAlternate(Esc) is pressed.
        /// </summary>
        public bool DisableAlternatePause { get; set; } = true;

        /// <summary>
        ///     The game won't spawn more NPC traffic if the limit is exceeded. -1 for unlimited (not recommended).
        /// </summary>
        public int WorldVehicleSoftLimit { get; set; } = 20;

        /// <summary>
        ///     The game won't spawn more NPC traffic if the limit is exceeded. -1 for unlimited (not recommended).
        /// </summary>
        public int WorldPedSoftLimit { get; set; } = 30;


        /// <summary>
        ///     Show the owner name of the entity you're aiming at
        /// </summary>
        public bool ShowEntityOwnerName { get; set; } = false;

        /// <summary>
        ///     Show other player's nametag on your screen, only effective if server didn't disable nametag display
        /// </summary>
        public bool ShowPlayerNameTag { get; set; } = true;

        /// <summary>
        ///     Show other player's blip on map, can be overridden by server resource
        /// </summary>
        public bool ShowPlayerBlip { get; set; } = true;
    }
}