﻿namespace Orleans.Clustering.Oracle
{
    public class TableVersionModel
    {
        public string ClusterId { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;

    }
    public class TableVersionData
    {
        public int Version { get; set; }
        public string VersionEtag { get; set; } = string.Empty;

    }
}
