namespace Yanjiu.Modules;
internal static class TravelRoute
{
    internal static (int Current, int Target, bool Back)[] Build(int current, int home, int target, string currentDc, string homeDc, string targetDc)
    {
        if (string.IsNullOrWhiteSpace(currentDc) || string.IsNullOrWhiteSpace(homeDc) || string.IsNullOrWhiteSpace(targetDc))
            throw new ArgumentException("无法确认角色的原始大区，已停止旅行。");
        return currentDc != homeDc && targetDc != homeDc
            ? [(current, home, true), (home, target, false)]
            : [(current, target, currentDc != homeDc && targetDc == homeDc)];
    }
}
