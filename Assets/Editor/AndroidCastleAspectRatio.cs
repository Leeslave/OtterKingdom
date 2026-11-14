#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Profile;

public sealed class AndroidCastleAspectRatio : IPostGenerateGradleAndroidProject
{
    private const string ProfilePath = "Assets/Settings/Build Profiles/Android - Castle APK.asset";
    // Android's manifest uses the long side divided by the short side.
    private const string PortraitAspect = "1.7777778";
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (AssetDatabase.GetAssetPath(BuildProfile.GetActiveBuildProfile()) != ProfilePath)
            return;

        // Unity's Player Settings clamp the maximum to 1.86. Android accepts 16 / 9.
        var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        var manifest = XDocument.Load(manifestPath);
        var activities = manifest.Descendants("activity")
            .Where(activity => activity.Elements("meta-data")
                .Any(metadata => (string)metadata.Attribute(Android + "name") == "unityplayer.UnityActivity"))
            .ToArray();
        if (activities.Length == 0)
            throw new BuildFailedException("The Castle APK manifest has no Unity player activity for the 9:16 aspect ratio.");

        foreach (var activity in activities)
        {
            activity.SetAttributeValue(Android + "minAspectRatio", PortraitAspect);
            activity.SetAttributeValue(Android + "maxAspectRatio", PortraitAspect);
            activity.SetAttributeValue(Android + "resizeableActivity", "false");
        }
        manifest.Save(manifestPath);
    }
}
#endif
