# Compile the actual fragment body using Windows' Direct3D compiler. This is not
# a Unity render test: only UnityCG's unchanged input struct is substituted.
$ErrorActionPreference = 'Stop'
if (-not ('WaterCausticsCompileTest' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WaterCausticsCompileTest {
    [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface Blob {
        [PreserveSig] IntPtr GetBufferPointer();
        [PreserveSig] UIntPtr GetBufferSize();
    }
    [DllImport("d3dcompiler_47.dll")]
    private static extern int D3DCompile(byte[] data, UIntPtr size, string file, IntPtr defines,
        IntPtr include, string entry, string target, uint flags1, uint flags2, out Blob code, out Blob errors);
    public static void Check(string source) {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(source);
        Blob code, errors;
        // Enable legacy sampler/tex2D compatibility, matching this Built-in CG pass.
        int result = D3DCompile(bytes, (UIntPtr)bytes.Length, "WaterCausticsScreen", IntPtr.Zero,
            IntPtr.Zero, "frag", "ps_4_0", 4096, 0, out code, out errors);
        string message = errors == null ? "" : Marshal.PtrToStringAnsi(errors.GetBufferPointer());
        if (code != null) Marshal.ReleaseComObject(code);
        if (errors != null) Marshal.ReleaseComObject(errors);
        if (result < 0) throw new InvalidOperationException(message);
        if (!String.IsNullOrEmpty(message)) Console.WriteLine(message);
    }
}
'@
}
$waterShaderPath = Join-Path $PSScriptRoot '../Assets/Resources/Shaders/WaterCausticsScreen.shader'
$waterShaderText = Get-Content -LiteralPath $waterShaderPath -Raw
$waterProgram = [regex]::Match($waterShaderText, '(?s)CGPROGRAM(.*?)ENDCG').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($waterProgram)) { throw 'CGPROGRAM not found.' }
$waterProgram = [regex]::Replace($waterProgram, '(?m)^\s*#pragma.*$', '')
$waterProgram = $waterProgram.Replace('#include "UnityCG.cginc"', 'struct v2f_img { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };')
[WaterCausticsCompileTest]::Check($waterProgram)
Write-Output 'PASS: WaterCausticsScreen fragment compiled for Direct3D (ps_4_0).'
