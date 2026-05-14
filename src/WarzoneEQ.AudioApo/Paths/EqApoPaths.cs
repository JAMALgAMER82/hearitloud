namespace WarzoneEQ.AudioApo.Paths;

public sealed record EqApoPaths(string RootDir)
{
    public static EqApoPaths Default => new(@"C:\Program Files\EqualizerAPO");

    public string ConfigDir          => Path.Combine(RootDir, "config");
    public string MasterConfigTxt    => Path.Combine(ConfigDir, "config.txt");
    public string WarzoneDir         => Path.Combine(ConfigDir, "warzone");
    public string CurrentTxt         => Path.Combine(WarzoneDir, "current.txt");
    public string ProfilesDir        => Path.Combine(WarzoneDir, "profiles");
    public string HeadphoneCorrDir   => Path.Combine(WarzoneDir, "headphone-correction");
    public string HrirDir            => Path.Combine(WarzoneDir, "hrir");
    public string JsfxDir            => Path.Combine(WarzoneDir, "jsfx");
    public string FpsCurvesDir       => Path.Combine(WarzoneDir, "fps-curves");
    public string ActiveHrirWav      => Path.Combine(HrirDir, "hesuvi-active.wav");
}
