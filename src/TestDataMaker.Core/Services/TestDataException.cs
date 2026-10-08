namespace TestDataMaker.Services;

/// <summary>
/// 利用者に原因を伝えるべきエラー。Message はそのまま画面に表示できる日本語にすること。
/// </summary>
public class TestDataException : Exception
{
    public TestDataException(string message) : base(message)
    {
    }

    public TestDataException(string message, Exception inner) : base(message, inner)
    {
    }
}
