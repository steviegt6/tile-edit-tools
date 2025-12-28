using System;
using Daybreak.Common.Features.Models;

namespace TileEditTools;

partial class ModImpl : INameProvider
{
    string INameProvider.GetName(Type type)
    {
        return NameProvider.GetNestedName(type);
    }
}
