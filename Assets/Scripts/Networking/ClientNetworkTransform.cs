// ---------------------------------------------------------------------------------------------
// ONLINE CO-OP FOUNDATION (Netcode for GameObjects).
//
// This file is guarded by the BLASTFRAME_NGO scripting define and compiles to NOTHING until:
//   1. com.unity.netcode.gameobjects is installed (blocked in the current environment by a TLS
//      certificate failure reaching packages.unity.com — see Handoffs/HANDOFF-008 report), AND
//   2. BLASTFRAME_NGO is added to Project Settings > Player > Scripting Define Symbols.
//
// It is written against the NGO 1.x API and has NOT been compile-verified (the package would not
// install). Treat it as reviewed scaffolding, not shipped-and-tested code.
// ---------------------------------------------------------------------------------------------
#if BLASTFRAME_NGO
using Unity.Netcode.Components;

namespace BlastFrame.Networking
{
    /// <summary>
    /// Owner-authoritative NetworkTransform. The built-in NetworkTransform is server-authoritative;
    /// overriding OnIsServerAuthoritative to false makes the OWNER the source of truth for position and
    /// rotation. This matches the client-side kinematic motor: the owner runs the full sweep and streams
    /// the result; non-owners just receive it (they never simulate). (NGO 2.x exposes this as an
    /// authority-mode enum on NetworkTransform directly.)
    /// </summary>
    [Unity.Netcode.DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
#endif
