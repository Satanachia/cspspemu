﻿using CSPspEmu.Hle.Attributes;

namespace CSPspEmu.Hle.Modules.stdio
{
    [HlePspModule(ModuleFlags = ModuleFlags.KernelMode | ModuleFlags.Flags0x00010011)]
    public class StdioForKernel : StdioForUser
    {
    }
}