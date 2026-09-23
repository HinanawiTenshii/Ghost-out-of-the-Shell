$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Rendering/StairFloorDirection.cs"
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
public static class StairDirectionTests {
 static int count;static int id=Shader.PropertyToID("_StairDirectionOverride");static int dir=Shader.PropertyToID("_Direction");
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
 static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static StairFloorDirection Make(Material mat){var c=new StairFloorDirection{renderer=new SpriteRenderer{sharedMaterial=mat}};Call(c,"Reset");Call(c,"OnEnable");Call(c,"Update");return c;}
 static float Effective(SpriteRenderer r){float v;return r.block.values.TryGetValue(id,out v)&&v>=0?v:r.sharedMaterial.GetFloat(dir);}
 public static int Run(){
  for(int i=0;i<4;i++){
   var preset=new Material();preset.values[dir]=i;var c=Make(preset);
   Check(Effective(c.renderer)==i,"All four authored orientations inherited");
   Check(c.Direction==(i<2?StairFloorDirection.StairDirection.Vertical:StairFloorDirection.StairDirection.Horizontal),"Inherited ascent axis");
   Check(c.Reverse==(i==1||i==2),"Inherited reversed ascent");
  }
  var mat=new Material();mat.values[dir]=0;var a=Make(mat);var b=Make(mat);
  int other=Shader.PropertyToID("_Unrelated");a.renderer.block.SetFloat(other,42);
  a.Direction=StairFloorDirection.StairDirection.Horizontal;
  Check(Effective(a.renderer)==3,"Horizontal ascends right");Check(Effective(b.renderer)==0,"Second renderer unaffected");
  a.Reverse=true;Check(Effective(a.renderer)==2,"Horizontal reverse ascends left");
  a.Direction=StairFloorDirection.StairDirection.Vertical;Check(Effective(a.renderer)==1,"Vertical reverse ascends down");
  a.Reverse=false;Check(Effective(a.renderer)==0,"Vertical ascends up");
  Check(a.renderer.block.values[other]==42,"Other properties preserved");
  Check(mat.GetFloat(dir)==0,"Shared material unchanged");Check(ReferenceEquals(a.renderer.sharedMaterial,b.renderer.sharedMaterial),"No material copies");
  int writes=a.renderer.writes;for(int i=0;i<20;i++)Call(a,"Update");Check(a.renderer.writes==writes,"No writes at rest");
  a.isActiveAndEnabled=false;Call(a,"OnDisable");Check(a.renderer.block.values[id]==-1,"Disable/removal restores sentinel");
  mat.values[dir]=2;Check(Effective(a.renderer)==2,"Disabled follows material edits");
  a.Direction=StairFloorDirection.StairDirection.Horizontal;a.Reverse=false;Check(Effective(a.renderer)==2,"Disabled setters do not override");
  a.isActiveAndEnabled=true;Call(a,"OnEnable");Call(a,"Update");Check(Effective(a.renderer)==3,"Enable reapplies saved choice");
  Check(a.renderer.block.values[other]==42,"Disable preserved other properties");
  typeof(StairFloorDirection).GetField("direction",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(a,StairFloorDirection.StairDirection.Vertical);
  writes=a.renderer.writes;Call(a,"OnValidate");Check(a.renderer.writes==writes,"Validation defers renderer work");
  Call(a,"Update");Check(Effective(a.renderer)==0,"Inspector/Undo serialized edits apply");
  var replacement=new Material();replacement.values[dir]=1;a.renderer.sharedMaterial=replacement;Call(a,"Update");
  Check(Effective(a.renderer)==0,"Material replacement retains object choice");
  a.Direction=(StairFloorDirection.StairDirection)99;Check(a.Direction==StairFloorDirection.StairDirection.Vertical,"Invalid enum normalized");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "PASS: $([StairDirectionTests]::Run()) production stair component checks (Unity renderer stubbed)."
