namespace WarzoneEQ.WindowsIntegration.Files;

public sealed class AtomicFileWriter : IConfigFileWriter
{
    public void Write(string targetPath, string contents)
    {
        var dir = Path.GetDirectoryName(targetPath)
                  ?? throw new ArgumentException("targetPath has no directory.", nameof(targetPath));
        Directory.CreateDirectory(dir);
        var temp = targetPath + ".tmp";
        File.WriteAllText(temp, contents, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(targetPath)) File.Replace(temp, targetPath, destinationBackupFileName: null);
        else File.Move(temp, targetPath);
    }
}
