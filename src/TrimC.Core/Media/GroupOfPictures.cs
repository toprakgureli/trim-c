// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;

namespace TrimC.Media
{
    /// <summary>
    /// A keyframe together with the number of video packets that belong to its group of pictures.
    /// </summary>
    /// <param name="Start">The presentation position of the keyframe, relative to <see cref="MediaInfo.StartTime"/>.</param>
    /// <param name="PacketCount">
    /// The number of video packets from this keyframe up to, but not including, the next keyframe in decode order.
    /// </param>
    public readonly record struct GroupOfPictures(TimeSpan Start, int PacketCount);
}
