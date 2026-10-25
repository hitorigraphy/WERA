namespace Wera.Web;

/// <summary>
/// 푸터의 "기본 게임팩 실행" 버튼과 홈 페이지를 잇는다. 버튼이 요청하면 홈 페이지가 기본 게임팩을 내려받아 올린다.
/// </summary>
public sealed class SampleLauncher
{
    /// <summary>홈 페이지가 구독한다. 구독자가 없으면 아무 일도 일어나지 않는다.</summary>
    public event Func<Task>? RunRequested;

    public Task RunAsync() => RunRequested?.Invoke() ?? Task.CompletedTask;
}
