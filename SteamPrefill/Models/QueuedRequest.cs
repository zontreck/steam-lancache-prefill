﻿namespace SteamPrefill.Models
{
    [ProtoContract(SkipConstructor = true)]
    public struct QueuedRequest
    {
        [ProtoMember(1)]
        public readonly uint DepotId;

        /// <summary>
        /// The SHA-1 hash of the chunk's id.
        /// </summary>
        [ProtoMember(2)]
        public string ChunkId;

        /// <summary>
        /// The content-length of the data to be requested.
        /// </summary>
        [ProtoMember(3)]
        public readonly uint CompressedLength;

        /// <summary>
        /// Adler-32 hash, always 4 bytes
        /// </summary>
        [ProtoMember(4)]
        public readonly byte[] Checksum;

        //TODO remove?
        [ProtoMember(5)]
        public readonly string ChecksumString;

        [ProtoMember(6)]
        public readonly byte[] DepotKey;

        public QueuedRequest(Manifest depotManifest, ChunkData chunk, byte[] depotKey)
        {
            DepotId = depotManifest.DepotId;
            ChunkId = chunk.ChunkId;
            CompressedLength = chunk.CompressedLength;

            Checksum = chunk.Checksum;
            ChecksumString = chunk.ChecksumString;

            DepotKey = depotKey;
        }

        public override string ToString()
        {
            return $"{DepotId} - {ChunkId}";
        }
    }
}