using Wera.Core;

namespace Wera.Core.Tests;

static class SessionPick
{
    /// <summary>목록 화면의 항목을 (줄 번호가 아니라) 항목 코드로 고른다: 그 항목이 있는 페이지로 가서 그 줄 번호를 입력한다.</summary>
    public static ScreenEntry? Pick(this GameSession session, string code) => session.Activate(session.DisplayCodeOf(code));
}
