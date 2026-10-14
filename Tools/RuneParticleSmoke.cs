using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class RuneParticleSmoke {
 public static void Run() {
  try {
   var go=new GameObject("Rune test");
   var component=go.AddComponent<GeometricFortressDecoration>();
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var type=component.GetType();
   type.GetField("shape",flags).SetValue(component,GeometricFortressDecoration.FurnitureShape.RuneSeal);
   type.GetField("accentColor",flags).SetValue(component,new Color(1,.14f,.08f,1));
   type.GetMethod("Rebuild",flags).Invoke(component,null);
   type.GetMethod("BuildRuneParticles",flags).Invoke(component,new object[]{Vector2.one});
   var ps=(ParticleSystem)type.GetField("runeParticles",flags).GetValue(component);
   ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=ps.main; main.startLifetime=4;main.startSize=1;main.startRotation=0;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
   var shape=ps.shape;shape.enabled=false;
   var velocity=ps.velocityOverLifetime;velocity.enabled=false;
   var rotation=ps.rotationOverLifetime;rotation.enabled=false;
   var emission=ps.emission;emission.enabled=false;
   ps.gameObject.layer=30;
   ps.Play();
   ps.Emit(new ParticleSystem.EmitParams {position=Vector3.zero,startSize=1,startLifetime=4,rotation=0},1);
   Debug.Log("RUNE_COUNT BEFORE "+ps.particleCount);
   ps.Simulate(1,true,false,true);
   Debug.Log("RUNE_COUNT AFTER "+ps.particleCount);
   var camera=new GameObject("Camera").AddComponent<Camera>();
   camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=.6f;
   camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;
   camera.backgroundColor=new Color(.02f,.04f,.15f,1);
   var renderer=ps.GetComponent<ParticleSystemRenderer>();
   var streams=new System.Collections.Generic.List<ParticleSystemVertexStream>();renderer.GetActiveVertexStreams(streams);
   var baked=new Mesh();renderer.BakeMesh(baked,camera,true);
   Debug.Log("RUNE_STREAMS "+string.Join(",",streams)+" BAKED_COLOR "+(baked.colors.Length>0?baked.colors[0].ToString():"NONE"));
   var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();
   RenderTexture.active=rt;var image=new Texture2D(256,256,TextureFormat.RGBA32,false);
   image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
   int red=0,blue=0,lit=0;float maxR=0,maxB=0;
   foreach(var c in image.GetPixels()){if(c.r>.20f||c.g>.20f||c.b>.30f){lit++;if(c.r>c.b*1.4f)red++;if(c.b>c.r*1.4f)blue++;maxR=Math.Max(maxR,c.r);maxB=Math.Max(maxB,c.b);}}
   System.IO.File.WriteAllBytes(Application.dataPath+"/../rune-render.png",image.EncodeToPNG());
   Debug.Log("RUNE_RENDER lit="+lit+" red="+red+" blue="+blue+" maxR="+maxR+" maxB="+maxB+" shader="+renderer.sharedMaterial.shader.name);
   if(lit==0 || red<lit*.95f || blue>0)throw new Exception("Rune particles must render red, not blue");
   if(baked.colors.Length==0 || baked.colors[0].r<.9f || baked.colors[0].b>.12f)throw new Exception("Particle vertex color packing regression");
   EditorApplication.Exit(0);
  } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
