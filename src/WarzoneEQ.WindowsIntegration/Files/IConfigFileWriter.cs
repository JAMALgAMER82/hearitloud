namespace WarzoneEQ.WindowsIntegration.Files;

public interface IConfigFileWriter
{
    void Write(string targetPath, string contents);
}
