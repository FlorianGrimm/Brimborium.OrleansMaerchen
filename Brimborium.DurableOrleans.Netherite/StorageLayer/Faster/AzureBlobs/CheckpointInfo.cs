// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite.Faster;

[JsonObject]
class CheckpointInfo
{
    [JsonProperty]
    public Guid IndexToken { get; set; }

    [JsonProperty]
    public Guid LogToken { get; set; }

    [JsonProperty]
    public long CommitLogPosition { get; set; }

    [JsonProperty]
    public long InputQueuePosition { get; set; }

    [JsonProperty]
    public int InputQueueBatchPosition { get; set; }

    [JsonProperty]
    public string InputQueueFingerprint { get; set; }

    [JsonProperty]
    public long NumberInstances { get; set; }
}
