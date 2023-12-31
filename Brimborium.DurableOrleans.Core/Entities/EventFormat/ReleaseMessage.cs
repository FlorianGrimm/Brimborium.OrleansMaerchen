﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace Orleans.DurableTask.Core.Entities.EventFormat;

[DataContract]
[GenerateSerializer]
[Alias("ReleaseMessage")]
public class ReleaseMessage : EntityMessage {
    [DataMember(Name = "parent")]
    [Id(0)]
    public string? ParentInstanceId { get; set; }

    [DataMember(Name = "id")]
    [Id(1)]
    public string? Id { get; set; }

    public override string GetShortDescription() {
        return $"[Release lock {this.Id} by {this.ParentInstanceId}]";
    }
}
