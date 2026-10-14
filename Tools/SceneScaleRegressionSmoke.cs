using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SceneScaleRegressionSmoke
{
    static IEnumerator suite; static int frame=-1;
    const BindingFlags F=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static SceneScaleRegressionSmoke(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool("SceneScaleRegressions",true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool("SceneScaleRegressions",false)){SessionState.SetBool("SceneScaleRegressions",false);suite=Verify();}
            if(suite==null||suite.MoveNext())return;
            suite=null;Debug.Log("SCENE_SCALE_REGRESSIONS_PASS");EditorApplication.Exit(0);
        }
        catch(Exception e){suite=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        foreach(Type test in new[]{typeof(PursuitSpacingSmoke),typeof(NearbyHostilitySmoke),typeof(SharedHostilitySmoke),typeof(MiniMapIncidentTimerSmoke),typeof(SquadPatrolSmoke)})
        {
            Debug.Log("REGRESSION_START "+test.Name);
            object result=test.GetMethod("Verify",F).Invoke(null,null);
            if(result is IEnumerator routine)while(routine.MoveNext())yield return routine.Current;
            test.GetMethod("Clear",F)?.Invoke(null,null);
            Debug.Log("REGRESSION_PASS "+test.Name+" checks="+test.GetField("checks",F)?.GetValue(null));
            yield return null;
        }
    }
}
