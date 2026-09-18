# Syntax regression check against Windows D3DCompiler, not a Unity rendering test.
$ErrorActionPreference = 'Stop'
if (-not ('SpriteWaterFlowCompileTest' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SpriteWaterFlowCompileTest {
    [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface Blob {
        [PreserveSig] IntPtr GetBufferPointer();
        [PreserveSig] UIntPtr GetBufferSize();
    }
    [DllImport("d3dcompiler_47.dll")]
    private static extern int D3DCompile(byte[] data, UIntPtr size, string file, IntPtr defines,
        IntPtr include, string entry, string target, uint flags1, uint flags2, out Blob code, out Blob errors);
    public static void Check(string source, string entry, string profile) {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(source);
        Blob code, errors;
        int result = D3DCompile(bytes, (UIntPtr)bytes.Length, "SpriteWaterFlow", IntPtr.Zero,
            IntPtr.Zero, entry, profile, 4096, 0, out code, out errors);
        string message = errors == null ? "" : Marshal.PtrToStringAnsi(errors.GetBufferPointer());
        if (code != null) Marshal.ReleaseComObject(code);
        if (errors != null) Marshal.ReleaseComObject(errors);
        if (result < 0) throw new InvalidOperationException(message);
        if (!String.IsNullOrEmpty(message)) Console.WriteLine(message);
    }
}
'@
}
$shaderText = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Assets/Resources/Shaders/SpriteWaterFlow.shader') -Raw
$program = [regex]::Match($shaderText, '(?s)CGPROGRAM(.*?)ENDCG').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($program)) { throw 'CGPROGRAM not found.' }
$program = [regex]::Replace($program, '(?m)^\s*#pragma.*$', '')
$program = $program.Replace('#include "UnityCG.cginc"', 'float4x4 unity_ObjectToWorld; float4x4 UNITY_MATRIX_V; float4 UnityObjectToClipPos(float4 v) { return v; }')
$program = $program.Replace('fixed4', 'float4')
[SpriteWaterFlowCompileTest]::Check($program, 'vert', 'vs_4_0')
[SpriteWaterFlowCompileTest]::Check($program, 'frag', 'ps_4_0')
Write-Output 'PASS: SpriteWaterFlow vertex and fragment compiled for Direct3D.'
