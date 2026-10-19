#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Info.plist values Unity's Player Settings can't express. The home-screen name is set here
// rather than through PlayerSettings.productName so desktop save paths (company/product) keep working.
public static class IosPostProcess
{
    public const string DisplayName = "Strike Swing";

    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetString("CFBundleDisplayName", DisplayName);
        // Only standard HTTPS/OS crypto, so App Store Connect can skip the export-compliance question.
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }
}
#endif
