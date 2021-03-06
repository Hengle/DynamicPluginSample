﻿using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class EditorBuildPluginCounter : ScriptableSingleton<EditorBuildPluginCounter>
{
	public int count = 100;
}

public class PluginBuilder
{
	private static System.Text.StringBuilder log_str_ = new System.Text.StringBuilder();

	private static void OutputHandler(object sender, System.Diagnostics.DataReceivedEventArgs args)
    {
        if (!string.IsNullOrEmpty(args.Data)) {
			log_str_.Append(args.Data);
			log_str_.Append("\n");
        }
    }

    private static void ErrorOutputHanlder(object sender, System.Diagnostics.DataReceivedEventArgs args)
    {
        if (!string.IsNullOrEmpty(args.Data)) {
			log_str_.Append(args.Data);
			log_str_.Append("\n");
        }
    }

	private static void clear_log()
	{
        var logEntries = System.Type.GetType("UnityEditorInternal.LogEntries,UnityEditor.dll");
        var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        clearMethod.Invoke(null, null);
	}

	private static bool run(string exec, string[] args)
	{
		log_str_ = new System.Text.StringBuilder();
		var process = new System.Diagnostics.Process();
		process.StartInfo.FileName = exec;
		process.StartInfo.RedirectStandardOutput = true;
		#if UNITY_EDITOR_WIN
		process.StartInfo.EnvironmentVariables["VisualStudioVersion"] = "14.0";
		#endif
		process.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(OutputHandler);
		process.StartInfo.UseShellExecute = false;
		process.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(ErrorOutputHanlder);
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.Arguments = string.Join(" ", args);
		process.EnableRaisingEvents = true;
		process.Start();
		process.BeginOutputReadLine();
		process.WaitForExit();
		if (log_str_.Length > 0) {
			log_str_.Append("done. "+ System.DateTime.Now + "\n");
			Debug.Log(log_str_.ToString());
		}
		var success = (process.ExitCode == 0);
		if (success) {
			Debug.LogFormat("[{0} {1}] {2}", exec, string.Join(" ", args), "done.");
		} else {
			Debug.LogErrorFormat("[{0} {1}] {2}", exec, string.Join(" ", args), "failed.");
		}
		return success;
	}

    [MenuItem ("Custom/Build iOS Plugin")]
	public static bool BuildPluginIOS()
	{
		Debug.Log("building iOS Plugin...");
		clear_log();
		var name = "UnityPlugin";
		var workdir = Application.dataPath+"/../PluginProjects/iOS";
		var deploydir = Application.dataPath+"/PluginWork/Plugins/iOS";
		var xcode_project_path = workdir + "/"+name+"/"+name+".xcodeproj";
		var success = false;
		success = run("xcodebuild", new string[] {
				"-verbose",
				"-project",
				xcode_project_path,
				"-target",
				name,
				"-configuration",
				"Release",
				"build"} );
		if (!success) {
			Debug.LogError("failed!");
			return false;
		}

		if (System.IO.Directory.Exists(deploydir)) {
			System.IO.Directory.Delete(deploydir, true /* recursive */);
		}
		System.IO.Directory.CreateDirectory(deploydir);

		var file = "lib"+name+".a";
		var build = workdir + "/UnityPlugin/build/Release-iphoneos/"+file;
		System.IO.File.Copy(build,
							deploydir + "/" + file);

		return true;
	}

    [MenuItem ("Custom/Build Android Plugin")]
	public static bool BuildPluginAndroid()
	{
		Debug.Log("building Android Plugin...");
		clear_log();
		
		EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
		var name = "UnityPlugin";
		var workdir = Application.dataPath+"/../PluginProjects/Android";
		var android_ndk_root = "/Applications/android-ndk-r10e";
		var deploydir = Application.dataPath+"/PluginWork/Plugins/Android";
		var sourcedir = Application.dataPath+"/PluginWork/Sources";
		var success = false;

		if (System.IO.Directory.Exists(workdir)) {
			System.IO.Directory.Delete(workdir, true /* recursive */);
		}
		System.IO.Directory.CreateDirectory(workdir);

		AssetDatabase.Refresh();

		string android_makefile = workdir+"/Android.mk";
		string application_makefile = workdir+"/Application.mk";

		{
			string AndroidMakefile_content = @"# This file is automatically generated. Don't touch!
include $(CLEAR_VARS)

LOCAL_ARM_MODE  := arm
LOCAL_PATH      := $(NDK_PROJECT_PATH)
LOCAL_MODULE    := lib{0}
LOCAL_CFLAGS    := -Werror
LOCAL_SRC_FILES := {1}
LOCAL_LDLIBS    := -llog

include $(BUILD_SHARED_LIBRARY)
";
			string source_files = sourcedir+"/interface.cpp";
			string str = string.Format(AndroidMakefile_content,
									   name,
									   source_files);
			using (var sw = new System.IO.StreamWriter(android_makefile)) {
				sw.Write(str);
			}
		}
		{
			string ApplicationMakefile_content = @"# This file is automatically generated. Don't touch!
APP_OPTIM        := release
APP_ABI          := armeabi
APP_PLATFORM     := android-8
APP_STL          := stlport_static
APP_BUILD_SCRIPT := {0}
";
			string str = string.Format(ApplicationMakefile_content,
									   android_makefile);
			using (var sw = new System.IO.StreamWriter(application_makefile)) {
				sw.Write(str);
			}
		}
		
		success = run(android_ndk_root+"/ndk-build", new string[] {
				"NDK_PROJECT_PATH="+workdir,
				"NDK_APPLICATION_MK="+application_makefile,
			} );
		if (!success) {
			Debug.LogError("failed!");
			return false;
		}

		if (System.IO.Directory.Exists(deploydir)) {
			System.IO.Directory.Delete(deploydir, true /* recursive */);
		}
		System.IO.Directory.CreateDirectory(deploydir);

		System.IO.File.Copy(workdir + "/libs/armeabi/lib"+name+".so",
							deploydir + "/lib"+name+".so");
		return true;
	}


    [MenuItem ("Custom/Build Editor Plugin &b")]
    public static void BuildPluginEditor()
	{
		clear_log();
		var name = "UnityPlugin";
		var workdir = Application.dataPath+"/../PluginProjects";
		var sourcedir = Application.dataPath+"/PluginWork/Sources";
		#if UNITY_EDITOR_WIN
		BuildPluginWin(name, workdir, sourcedir);
		#elif UNITY_EDITOR_OSX
		BuildPluginOSX(name, workdir, sourcedir);
		#endif
		Debug.Log("Done. "+ System.DateTime.Now);
		AssetDatabase.Refresh();
	}

	private static void BuildPluginWin(string name, string workdir, string sourcedir)
	{
		Debug.Log("building Windows Editor Plugin...");
		var deploydir = Application.dataPath+"/PluginWork/Plugins/x86_64/"+name;

		var vs_project_path = workdir + "/Windows/"+name+"/"+name+"/"+name+".vcxproj";
		var success = false;
		success = run(@"c:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe",
					  new string[] {
						  "/p:Configuration=Release",
						  "/p:Platform=x64",
						  "/p:PlatformToolset=v140",
						  vs_project_path} );
		if (!success) {
			Debug.LogError("failed!");
			return;
		}

		if (System.IO.Directory.Exists(deploydir)) {
			System.IO.Directory.Delete(deploydir, true /* recursive */);
		}
		System.IO.Directory.CreateDirectory(deploydir);
		System.IO.File.Copy(workdir + "/Windows/"+name+"/"+name+"/x64/Release/"+name+".dll",
							deploydir + "/" + name + ".dll");

	}

	private static void BuildPluginOSX(string name, string workdir, string sourcedir)
	{
		var id_str = System.DateTime.Now.ToString("yyyyMMddHHmmss");
		Debug.LogFormat("building OSX Plugin[{0}]...", id_str);
		var deploydir = Application.dataPath+"/PluginWork/Plugins/OSX";
		var xcode_project_path = workdir + "/OSX/"+name+"/"+name+".xcodeproj";
		var success = false;
		success = run("xcodebuild", new string[] {
				"-verbose",
				"-project",
				xcode_project_path,
				"-target",
				name,
				"-configuration",
				"Debug",
				"build"} );
		if (!success) {
			Debug.LogError("failed!");
			return;
		}

		if (System.IO.Directory.Exists(deploydir)) {
			System.IO.Directory.Delete(deploydir, true /* recursive */);
		}
		System.IO.Directory.CreateDirectory(deploydir);

		// no CopyDirectory API on .NET 
		success = run("cp", new string[] {
				"-rf",
				workdir+"/OSX/"+name+"/build/Debug/"+name+".bundle",
				deploydir+"/"+name+".bundle",
			});
		if (!success) {
			Debug.LogError("failed!");
			return;
		}
	}

    [UnityEditor.Callbacks.PostProcessBuildAttribute(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		switch (target) {
		case BuildTarget.iOS:
			var ios_path = Application.dataPath+"/PluginWork/Plugins/iOS/libUnityPlugin.a";
			if (!System.IO.File.Exists(ios_path)) {
				BuildPluginIOS();
				EditorUtility.DisplayDialog("iOS plugin was built now", "Please rebuild again!.", "OK");
			}
			break;
		case BuildTarget.Android:
			var android_path = Application.dataPath+"/PluginWork/Plugins/Android/libUnityPlugin.so";
			if (!System.IO.File.Exists(android_path)) {
				BuildPluginAndroid();
				EditorUtility.DisplayDialog("Android plugin was built now", "Please rebuild again!.", "OK");
				EditorUtility.DisplayDialog("need to do", "import settings for the plugin\nAssets/PluginWork/Plugins/Android/libUnityPlugin.so\nis needed to be properly setup.", "OK");
			}
			break;
		}
	}
}
/*
 * End of PluginBuilder.cs
 */
