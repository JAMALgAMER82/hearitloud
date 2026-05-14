namespace WarzoneEQ.WindowsIntegration.LoudnessEq;

public interface ILoudnessEqController
{
    LoudnessEqState? Read(string endpointGuid);
    void Write(string endpointGuid, LoudnessEqState state);
}
