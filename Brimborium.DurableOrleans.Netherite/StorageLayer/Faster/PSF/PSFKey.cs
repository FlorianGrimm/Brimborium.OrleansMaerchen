﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite.Faster;

struct PSFKey : IFasterEqualityComparer<PSFKey> {
    internal static TimeSpan DateBinInterval = TimeSpan.FromMinutes(1);
    internal const int InstanceIdPrefixLen = 7;

    enum PsfColumn { RuntimeStatus = 101, CreatedTime, InstanceIdPrefix }

    // Enums are not blittable
    readonly int column;
    readonly int value;

    internal PSFKey(OrchestrationStatus status) {
        this.column = (int)PsfColumn.RuntimeStatus;
        this.value = (int)status;
    }

    internal PSFKey(DateTime dt) {
        this.column = (int)PsfColumn.CreatedTime;

        // Make bins of one minute, starting from the beginning of 2020; there are 527040 minutes in a leap year, 1440 in a day.
        // If we're still using this in 1000 years I will be amazed. TODO confirm the one-minute bin interval, or make it configurable
        var year = dt.Year - 2020;
        this.value = (year * 1_000_000) + (dt.DayOfYear * 1440) + (dt.Hour * 60) + dt.Minute;
    }

    internal PSFKey(string instanceId, int prefixLength = InstanceIdPrefixLen)    // TODO change this to pass a list of prefixFunc<string, string> and make a PSF for each? E.g. parse "@{entityName.ToLowerInvariant()}@" or "@"
    {
        this.column = (int)PsfColumn.InstanceIdPrefix;
        if (instanceId.Length > prefixLength) {
            instanceId = instanceId.Substring(0, Math.Min(instanceId.Length, prefixLength));
        }
        this.value = GetInvariantHashCode(instanceId);
    }

    static int GetInvariantHashCode(string item) {
        // NetCore randomizes string.GetHashCode() per-appdomain, to prevent hash flooding.
        // Therefore it's important to verify for each call site that this isn't a concern.
        // This is a non-unsafe/unchecked version of (internal) string.GetLegacyNonRandomizedHashCode().
        unchecked {
            int hash1 = (5381 << 16) + 5381;
            int hash2 = hash1;

            for (var ii = 0; ii < item.Length; ii += 2) {
                hash1 = ((hash1 << 5) + hash1) ^ item[ii];
                if (ii < item.Length - 1) {
                    hash2 = ((hash2 << 5) + hash2) ^ item[ii + 1];
                }
            }
            return hash1 + (hash2 * 1566083941);
        }
    }

    OrchestrationStatus Status => (OrchestrationStatus)this.value;

    public bool Equals(ref PSFKey k1, ref PSFKey k2) => k1.column == k2.column && k1.value == k2.value;

    public long GetHashCode64(ref PSFKey k) => Utility.GetHashCode(this.column) ^ Utility.GetHashCode(this.value);

    public override string ToString() => this.Status.ToString();
}
