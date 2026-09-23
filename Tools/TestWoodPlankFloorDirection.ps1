$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Rendering/WoodPlankFloorDirection.cs"
# Exercise the entire production component; only Unity renderer/lifecycle APIs are stubbed.
$code = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
 public class ExecuteAlways:Attribute{} public class DisallowMultipleComponent:Attribute{}
 public class RequireComponent:Attribute{public RequireComponent(Type t){}}
 public class AddComponentMenu:Attribute{public AddComponentMenu(string s){}}
 public class SerializeField:Attribute{} public class Tooltip:Attribute{public Tooltip(string s){}}
 public class InspectorName:Attribute{public InspectorName(string s){}}
 public class ContextMenu:Attribute{public ContextMenu(string s){}}
 public class MonoBehaviour{public SpriteRenderer renderer;public bool isActiveAndEnabled=true;public T GetComponent<T>() where T:class=>renderer as T;}
 public static class Shader{static Dictionary<string,int> ids=new Dictionary<string,int>();public static int PropertyToID(string s){if(!ids.ContainsKey(s))ids[s]=ids.Count;return ids[s];}}
 public class Material{public Dictionary<int,float> values=new Dictionary<int,float>();public bool HasProperty(int id)=>values.ContainsKey(id);public float GetFloat(int id)=>values[id];}
 public class MaterialPropertyBlock{public Dictionary<int,float> values=new Dictionary<int,float>();public void SetFloat(int id,float v)=>values[id]=v;}
 public class SpriteRenderer {
  public Material sharedMaterial;public MaterialPropertyBlock block=new MaterialPropertyBlock();public int writes;
  public void GetPropertyBlock(MaterialPropertyBlock p){p.values=new Dictionary<int,float>(block.values);}
  public void SetPropertyBlock(MaterialPropertyBlock p){block.values=new Dictionary<int,float>(p.values);writes++;}
 }
}
'@
$code += $source.Replace('using UnityEngine;', '')
$code += @'
public static class PlankDirectionTests {
 static int count;static int id=Shader.PropertyToID("_DirectionOverride");static int dir=Shader.PropertyToID("_Direction");
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
 static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static WoodPlankFloorDirection Make(Material mat){var c=new WoodPlankFloorDirection{renderer=new SpriteRenderer{sharedMaterial=mat}};Call(c,"Reset");Call(c,"OnEnable");Call(c,"Update");return c;}
 static float Effective(SpriteRenderer r){float v;return r.block.values.TryGetValue(id,out v)&&v>=0?v:r.sharedMaterial.GetFloat(dir);}
 public static int Run(){
  var mat=new Material();mat.values[dir]=0;
  var a=Make(mat);var b=Make(mat);int unrelated=Shader.PropertyToID("_Unrelated");a.renderer.block.SetFloat(unrelated,42);
  a.Direction=WoodPlankFloorDirection.BoardDirection.Vertical;
  Check(Effective(a.renderer)==1,"A vertical");Check(Effective(b.renderer)==0,"B remains horizontal");
  Check(mat.GetFloat(dir)==0,"Shared material unchanged");Check(ReferenceEquals(a.renderer.sharedMaterial,b.renderer.sharedMaterial),"No material clones");
  Check(a.renderer.block.values[unrelated]==42,"Unrelated properties retained");
  int n=a.renderer.writes;for(int i=0;i<20;i++)Call(a,"Update");Check(a.renderer.writes==n,"No writes while unchanged");
  a.isActiveAndEnabled=false;Call(a,"OnDisable");Check(Effective(a.renderer)==0,"Disable returns to default");
  Check(a.renderer.block.values[unrelated]==42,"Disable preserves other properties");mat.values[dir]=1;Check(Effective(a.renderer)==1,"Disabled follows live material default");
  a.Direction=WoodPlankFloorDirection.BoardDirection.Horizontal;Check(Effective(a.renderer)==1,"Disabled setter does not override");
  a.isActiveAndEnabled=true;Call(a,"OnEnable");Call(a,"Update");Check(Effective(a.renderer)==0,"Enable restores chosen setting");
  var c=Make(mat);Check(c.Direction==WoodPlankFloorDirection.BoardDirection.Vertical,"New component inherits material orientation");
  typeof(WoodPlankFloorDirection).GetField("direction",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,WoodPlankFloorDirection.BoardDirection.Horizontal);
  n=c.renderer.writes;Call(c,"OnValidate");Check(c.renderer.writes==n,"Validation defers renderer calls");Call(c,"Update");Check(Effective(c.renderer)==0,"Inspector and Undo serialized updates apply");
  var replacement=new Material();replacement.values[dir]=0;c.renderer.sharedMaterial=replacement;Call(c,"Update");Check(Effective(c.renderer)==0,"Material replacement retains object choice");
  c.Direction=(WoodPlankFloorDirection.BoardDirection)99;Check(c.Direction==WoodPlankFloorDirection.BoardDirection.Horizontal,"Invalid runtime enum normalized");
  Call(c,"OnDisable");Check(c.renderer.block.values[id]==-1,"Removal/disable clears override via sentinel");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "PASS: $([PlankDirectionTests]::Run()) production component checks (Unity renderer stubbed)."
