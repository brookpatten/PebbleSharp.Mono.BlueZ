﻿using System;

namespace PebbleSharp.Core
{
    public class FirmwareVersion
    {
        public FirmwareVersion( DateTime timestamp, string version, string commit,
                                bool isRecovery, Hardware hardwarePlatform, byte metadataVersion )
        {
            Timestamp = timestamp;
            Version = version;
            Commit = commit;
            IsRecovery = isRecovery;
            HardwarePlatform = hardwarePlatform;
            MetadataVersion = metadataVersion;
        }

        public DateTime Timestamp { get; private set; }
        public string Version { get; private set; }
        public string Commit { get; private set; }
        public bool IsRecovery { get; private set; }
		public Hardware HardwarePlatform { get; private set; }
        public byte MetadataVersion { get; private set; }

        public override string ToString()
        {
            const string format = "Version {0}, commit {1} ({2})\n"
                                  + "Recovery:         {3}\n"
                                  + "HW Platform:      {4}\n"
                                  + "Metadata version: {5}";
            return string.Format(format, Version, Commit, Timestamp, IsRecovery, HardwarePlatform, MetadataVersion);
        }
    }
}